namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates per-segment TypeScript client classes from OpenAPI operations.
/// </summary>
[SuppressMessage("Design", "MA0051:Method is too long", Justification = "Code generation methods require sequential StringBuilder operations.")]
public static class TypeScriptClientExtractor
{
    /// <summary>
    /// Extracts all segment client classes from the OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The parsed OpenAPI document.</param>
    /// <param name="headerContent">Optional auto-generated file header.</param>
    /// <param name="enumNames">Names of types that are enums (for correct import type).</param>
    /// <returns>List of (ClassName, FileContent) tuples for each segment client.</returns>
    public static List<(string ClassName, string Content)> Extract(
        OpenApiDocument openApiDoc,
        string? headerContent,
        HashSet<string>? enumNames = null,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase,
        bool convertDates = false,
        TypeScriptHttpClient httpClient = TypeScriptHttpClient.Fetch,
        HashSet<string>? writableSchemas = null,
        bool brandedIds = false,
        bool zodRuntimeValidate = false)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        var results = new List<(string ClassName, string Content)>();
        var segments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);
        writableSchemas ??= new HashSet<string>(StringComparer.Ordinal);

        foreach (var segment in segments)
        {
            var operations = PathSegmentHelper.GetOperationsForSegment(openApiDoc, segment);
            if (operations.Count == 0)
            {
                continue;
            }

            var className = segment + "Client";
            var content = GenerateClientClass(className, operations, openApiDoc, headerContent, enumNames, namingStrategy, convertDates, httpClient, writableSchemas, brandedIds, zodRuntimeValidate);
            results.Add((className, content));
        }

        return results;
    }

    private static string GenerateClientClass(
        string className,
        List<(string Path, string Method, OpenApiOperation Operation)> operations,
        OpenApiDocument openApiDoc,
        string? headerContent,
        HashSet<string>? enumNames,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        TypeScriptHttpClient httpClient,
        HashSet<string> writableSchemas,
        bool brandedIds,
        bool zodRuntimeValidate)
    {
        var sb = new StringBuilder();
        var importTypes = new HashSet<string>(StringComparer.Ordinal);
        var brandImports = new SortedSet<string>(StringComparer.Ordinal);

        // Per-class accumulator for the Zod schemas this client file references.
        // Drives both the `import { PetSchema } from '../models/Pet.zod'` lines and
        // the conditional `import { z } from 'zod'` when any method wraps inline.
        var zodSchemaImports = new SortedSet<string>(StringComparer.Ordinal);
        var needsZodImport = false;

        // First pass: collect all import types
        foreach (var (operationPath, _, operation) in operations)
        {
            TypeScriptOperationHelper.CollectImportTypes(operation, importTypes, openApiDoc, operationPath);
        }

        // For schemas that have a Writable sibling (readOnly/writeOnly split), an operation
        // that posts/puts that schema needs the `<Name>Writable` variant imported alongside
        // — the response-side import keeps the canonical name. Add the suffixed names so
        // the body-type emission below can name them.
        foreach (var (_, _, operation) in operations)
        {
            var (bodySchema, _) = operation.GetRequestBodySchemaWithContentType();
            if (bodySchema is OpenApiSchemaReference bodyRef)
            {
                var refName = bodyRef.Reference.Id ?? bodyRef.Id;
                if (refName != null && writableSchemas.Contains(refName))
                {
                    importTypes.Add(refName + TypeScriptModelExtractor.WritableSuffix);
                }
            }
        }

        // Second pass: fix imports for streaming operations whose response schema
        // is a $ref to an array type (e.g., Accounts -> Account[]).
        // Add the item type import; only remove the wrapper if no non-streaming
        // operation also references it.
        foreach (var (_, _, operation) in operations)
        {
            if (!operation.IsStreamingResponse())
            {
                continue;
            }

            var schema = operation.GetResponseSchema("200") ?? operation.GetResponseSchema("201");
            if (schema is OpenApiSchemaReference streamingRef)
            {
                var resolved = streamingRef.Target;
                if (resolved is OpenApiSchema resolvedSchema &&
                    resolvedSchema.Type?.HasFlag(JsonSchemaType.Array) == true)
                {
                    var wrapperName = streamingRef.Reference.Id ?? streamingRef.Id;

                    // Check if any non-streaming operation also uses this wrapper type
                    var usedByNonStreaming = false;
                    if (wrapperName != null)
                    {
                        foreach (var (otherPath, _, otherOp) in operations)
                        {
                            if (otherOp == operation || otherOp.IsStreamingResponse())
                            {
                                continue;
                            }

                            var otherImports = new HashSet<string>(StringComparer.Ordinal);
                            TypeScriptOperationHelper.CollectImportTypes(otherOp, otherImports, openApiDoc, otherPath);
                            if (otherImports.Contains(wrapperName))
                            {
                                usedByNonStreaming = true;
                                break;
                            }
                        }

                        if (!usedByNonStreaming)
                        {
                            importTypes.Remove(wrapperName);
                        }
                    }

                    // Add the item type (e.g., "Account")
                    if (resolvedSchema.Items is OpenApiSchemaReference itemRef)
                    {
                        var itemName = itemRef.Reference.Id ?? itemRef.Id;
                        if (itemName != null)
                        {
                            importTypes.Add(itemName);
                        }
                    }
                }
            }
        }

        // Per-operation result-type aliases: one bespoke discriminated union per non-streaming
        // method, with arms keyed off the operation's declared response codes. Computed first
        // so their referenced model/error types get folded into the file-level import set.
        // Paginated-streaming ops additionally get a `<MethodName>PageResult` alias for the
        // non-streaming companion method that feeds useInfiniteQuery.
        var perOpResultTypes = new Dictionary<OpenApiOperation, string>(ReferenceEqualityComparer.Instance);
        var perOpPageResultTypes = new Dictionary<OpenApiOperation, string>(ReferenceEqualityComparer.Instance);
        var perOpDeclarations = new List<string>();
        var perOpErrorImports = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (operationPath, methodVerb, operation) in operations)
        {
            var isStreaming = operation.IsStreamingResponse();
            var isPaginatedStreaming = isStreaming && operation.IsPaginatedStreamingOperation();

            if (isStreaming && !isPaginatedStreaming)
            {
                // Pure streaming ops return AsyncGenerator<Item> directly — no result alias.
                continue;
            }

            var isFileDownload = operation.HasFileDownload();
            var isTextDownload = !isFileDownload && operation.HasTextResponse();
            var operationId = operation.GetOperationId(operationPath, methodVerb);
            var methodName = operationId.ToCamelCase().ToTypeScriptIdentifier();

            if (isPaginatedStreaming)
            {
                // Only the Page companion needs a per-op result type. The streaming method
                // itself stays an AsyncGenerator. Name: `<MethodName>PageResult`.
                var pageResultTypeName = methodName.ToPascalCase() + "PageResult";
                var (pageDeclaration, pageImports) = TypeScriptOperationHelper.BuildPerOperationResultType(
                    operation,
                    pageResultTypeName,
                    isFileDownload: false,
                    isTextDownload: false,
                    httpClient);
                perOpPageResultTypes[operation] = pageResultTypeName;
                perOpDeclarations.Add(pageDeclaration);
                foreach (var imp in pageImports)
                {
                    if (imp is "ApiError" or "ValidationError")
                    {
                        perOpErrorImports.Add(imp);
                    }
                    else
                    {
                        importTypes.Add(imp);
                    }
                }

                continue;
            }

            var resultTypeName = methodName.ToPascalCase() + "Result";

            var (declaration, imports) = TypeScriptOperationHelper.BuildPerOperationResultType(
                operation,
                resultTypeName,
                isFileDownload,
                isTextDownload,
                httpClient);

            perOpResultTypes[operation] = resultTypeName;
            perOpDeclarations.Add(declaration);

            foreach (var imp in imports)
            {
                if (imp is "ApiError" or "ValidationError")
                {
                    perOpErrorImports.Add(imp);
                }
                else
                {
                    importTypes.Add(imp);
                }
            }
        }

        // Pre-scan path parameters for branded ID brands so the import line is
        // accurate before the class body lands in the StringBuilder.
        if (brandedIds)
        {
            foreach (var brand in CollectBrandImports(operations, openApiDoc))
            {
                brandImports.Add(brand);
            }
        }

        // Pre-scan response schemas for runtime Zod validation. The schema imports
        // need to land in the file header before any method body references them.
        if (zodRuntimeValidate)
        {
            foreach (var (_, _, operation) in operations)
            {
                var spec = TypeScriptOperationHelper.TryGetResponseZodSchemaSpec(operation);
                if (spec == null)
                {
                    continue;
                }

                foreach (var name in spec.RefSchemaNames)
                {
                    zodSchemaImports.Add(name);
                }

                if (spec.NeedsZodImport)
                {
                    needsZodImport = true;
                }
            }
        }

        // Build the file body (per-op result aliases + class) BEFORE the imports so the
        // import line can be narrowed to identifiers the body actually references. Without
        // this, a model/enum collected from the schema graph but never surfaced in a
        // signature/result-type — or AxiosResponse on a pure-streaming-only client — would
        // import for nothing and fail strict tsc under noUnusedLocals.
        var bodySb = new StringBuilder();

        // Emit per-operation result-type aliases before the class.
        foreach (var declaration in perOpDeclarations)
        {
            bodySb.Append(declaration);
            bodySb.AppendLine();
        }

        // Class declaration
        bodySb.Append("export class ").Append(className).AppendLine(" {");
        bodySb.AppendLine("  private readonly api: ApiClient;");
        bodySb.AppendLine();
        bodySb.AppendLine("  constructor(api: ApiClient) {");
        bodySb.AppendLine("    this.api = api;");
        bodySb.AppendLine("  }");

        // Generate methods
        foreach (var (path, method, operation) in operations)
        {
            bodySb.AppendLine();
            perOpResultTypes.TryGetValue(operation, out var resultTypeName);
            perOpPageResultTypes.TryGetValue(operation, out var pageResultTypeName);
            AppendMethod(bodySb, path, method, operation, openApiDoc, namingStrategy, convertDates, resultTypeName, writableSchemas, pageResultTypeName, brandedIds, zodRuntimeValidate);
        }

        bodySb.AppendLine("}");

        var bodyText = bodySb.ToString();

        // Write header
        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        // Write imports (narrowed to what the body references)
        AppendImports(sb, importTypes, enumNames, perOpErrorImports, httpClient, brandImports, zodSchemaImports, needsZodImport, bodyText);

        sb.Append(bodyText);

        return sb.ToString();
    }

    /// <summary>
    /// Pre-scan: every path parameter on every operation in the segment is checked
    /// for brand qualification. The resulting set drives both the per-class import
    /// line and what gets substituted in parameter signatures downstream.
    /// </summary>
    private static SortedSet<string> CollectBrandImports(
        List<(string Path, string Method, OpenApiOperation Operation)> operations,
        OpenApiDocument openApiDoc)
    {
        var brands = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (path, _, operation) in operations)
        {
            var pathParams = TypeScriptOperationHelper.GetMergedParameters(operation, openApiDoc, path, ParameterLocation.Path);
            foreach (var param in pathParams)
            {
                if (string.IsNullOrEmpty(param.Name))
                {
                    continue;
                }

                var brand = TypeScriptBrandedIdExtractor.ResolveParamBrand(path, param.Name!, param.Schema);
                if (brand != null)
                {
                    brands.Add(brand);
                }
            }
        }

        return brands;
    }

    private static void AppendImports(
        StringBuilder sb,
        HashSet<string> importTypes,
        HashSet<string>? enumNames,
        HashSet<string> errorImports,
        TypeScriptHttpClient httpClient,
        SortedSet<string> brandImports,
        SortedSet<string> zodSchemaImports,
        bool needsZodImport,
        string bodyText)
    {
        // The Axios variant references AxiosResponse in per-op result-type arms — the Fetch
        // variant uses the global Response type. A client with no per-op result type (e.g.
        // only pure-streaming ops) never references it, so gate the import on actual usage
        // to avoid an unused-import tsc error.
        if (httpClient == TypeScriptHttpClient.Axios && TypeScriptOperationHelper.ReferencesIdentifier(bodyText, "AxiosResponse"))
        {
            sb.AppendLine("import type { AxiosResponse } from 'axios';");
        }

        // Import ApiClient
        sb.AppendLine("import { ApiClient } from './ApiClient';");

        // Build model imports and enum imports separately. Only types the body actually
        // references are imported — the schema-graph scan can over-collect (e.g. an enum
        // reachable only through an inline request-body property, or an array-alias wrapper
        // for a streamed item type) which would otherwise fail tsc under noUnusedLocals.
        var modelImports = new SortedSet<string>(StringComparer.Ordinal);
        var enumImports = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var typeName in importTypes)
        {
            if (!TypeScriptOperationHelper.ReferencesIdentifier(bodyText, typeName))
            {
                continue;
            }

            if (enumNames != null && enumNames.Contains(typeName))
            {
                enumImports.Add(typeName);
            }
            else
            {
                modelImports.Add(typeName);
            }
        }

        if (modelImports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", modelImports)).AppendLine(" } from '../models';");
        }

        if (enumImports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", enumImports)).AppendLine(" } from '../enums';");
        }

        if (errorImports.Count > 0)
        {
            var sorted = new SortedSet<string>(errorImports, StringComparer.Ordinal);
            sb.Append("import type { ").Append(string.Join(", ", sorted)).AppendLine(" } from '../errors';");
        }

        if (brandImports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", brandImports)).AppendLine(" } from '../types/BrandedIds';");
        }

        // Runtime Zod validation: one import line per response schema the methods
        // reference, plus the `z` runtime import when any method wraps inline
        // (z.array, z.string, etc).
        if (needsZodImport)
        {
            sb.AppendLine("import { z } from 'zod';");
        }

        foreach (var schemaName in zodSchemaImports)
        {
            sb.Append("import { ").Append(schemaName).Append("Schema } from '../models/").Append(schemaName).AppendLine(".zod';");
        }

        sb.AppendLine();
    }

    private static void AppendMethod(
        StringBuilder sb,
        string path,
        string httpMethod,
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        string? perOpResultTypeName,
        HashSet<string> writableSchemas,
        string? perOpPageResultTypeName,
        bool brandedIds,
        bool zodRuntimeValidate)
    {
        var isStreaming = operation.IsStreamingResponse();
        var isFileDownload = operation.HasFileDownload();
        var isFileUpload = operation.HasFileUpload();
        var isTextDownload = !isFileDownload && operation.HasTextResponse();
        var operationId = operation.GetOperationId(path, httpMethod);
        var methodName = operationId.ToCamelCase().ToTypeScriptIdentifier();

        // Get parameters (merge path-level and operation-level)
        var pathParams = TypeScriptOperationHelper.GetMergedParameters(operation, openApiDoc, path, ParameterLocation.Path);
        var queryParams = TypeScriptOperationHelper.GetMergedParameters(operation, openApiDoc, path, ParameterLocation.Query);
        var headerParams = TypeScriptOperationHelper.GetMergedParameters(operation, openApiDoc, path, ParameterLocation.Header);

        // Get request body
        var (bodySchema, bodyContentType) = operation.GetRequestBodySchemaWithContentType();

        // Get response type
        var returnType = TypeScriptOperationHelper.GetReturnType(operation, isStreaming, isFileDownload);

        // Emit a /** ... */ block when the spec has anything worth saying
        // about this operation — summary, description, or deprecated marker. The same
        // block applies to the Page companion below (it shares the operation).
        AppendOperationJsDoc(sb, operation);

        if (isStreaming)
        {
            AppendStreamingMethod(sb, methodName, path, pathParams, queryParams, headerParams, returnType, namingStrategy, convertDates, brandedIds);

            // Paginated-streaming ops also get a non-streaming Page companion that
            // returns one page of results for useInfiniteQuery to consume. The page return
            // type is the full response schema (PaginationResult<Item>), not the streaming
            // item type, so re-compute via the non-streaming path of GetReturnType.
            if (perOpPageResultTypeName != null)
            {
                var pageDataType = TypeScriptOperationHelper.GetReturnType(operation, isStreaming: false, isFileDownload: false);
                AppendOperationJsDoc(sb, operation);
                AppendPageCompanionMethod(sb, methodName, path, pathParams, queryParams, headerParams, pageDataType, perOpPageResultTypeName, namingStrategy, convertDates, brandedIds);
            }
        }
        else
        {
            // Compute the Zod schema spec for this op's response (if any). Threaded
            // through so AppendStandardMethod can splice `parseSchema: <expr>` into
            // the request options bag.
            var zodSpec = zodRuntimeValidate
                ? TypeScriptOperationHelper.TryGetResponseZodSchemaSpec(operation)
                : null;
            AppendStandardMethod(sb, methodName, path, httpMethod, pathParams, queryParams, headerParams, bodySchema, bodyContentType, isFileUpload, isFileDownload, isTextDownload, returnType, namingStrategy, convertDates, perOpResultTypeName, writableSchemas, brandedIds, zodSpec);
        }
    }

    /// <summary>
    /// Emits a 2-space-indented JSDoc block above a client method when the OpenAPI
    /// operation carries a summary, description, or <c>deprecated: true</c> flag.
    /// Summary wins over description if both are present — the summary is the canonical
    /// one-liner that appears in Swagger UI; the description tends to be paragraphs.
    /// </summary>
    private static void AppendOperationJsDoc(
        StringBuilder sb,
        OpenApiOperation operation)
    {
        var description = !string.IsNullOrWhiteSpace(operation.Summary)
            ? operation.Summary
            : operation.Description;

        if (string.IsNullOrWhiteSpace(description) && !operation.Deprecated)
        {
            return;
        }

        var jsDoc = new JsDocComment(
            description: description,
            parameters: null,
            returns: null,
            isDeprecated: operation.Deprecated,
            deprecatedMessage: null,
            example: null);

        var rendered = new JsDocCommentGenerator().GenerateTags(indentSpaces: 2, jsDoc);
        if (!string.IsNullOrEmpty(rendered))
        {
            sb.Append(rendered);
        }
    }

    /// <summary>
    /// Emits the non-streaming Page companion for a paginated-streaming operation: same
    /// path / query / header params as the streaming sibling PLUS a synthesized
    /// <c>headers?: { 'x-continuation'?: string }</c> so useInfiniteQuery's
    /// <c>fetchNextPage</c> can pass the continuation token from the previous page.
    /// Returns <c>Promise&lt;&lt;MethodName&gt;PageResult&gt;</c> (the per-op result type
    /// alias computed in GenerateClientClass).
    /// </summary>
    private static void AppendPageCompanionMethod(
        StringBuilder sb,
        string streamingMethodName,
        string path,
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        List<OpenApiParameter> headerParams,
        string pageDataType,
        string pageResultTypeName,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        var pageMethodName = streamingMethodName + "Page";

        // Build parameter list. We merge the spec-declared headers with a synthesized
        // 'x-continuation' header so the consumer can pass the token without the spec
        // needing to declare it explicitly.
        var paramParts = new List<string>();
        foreach (var p in pathParams)
        {
            var n = (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var t = TypeScriptOperationHelper.GetParameterType(p, convertDates, brandedIds, path);
            paramParts.Add(n + ": " + t);
        }

        if (queryParams.Count > 0)
        {
            var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(queryParams, namingStrategy, convertDates);
            paramParts.Add("query?: " + queryType);
        }

        // The continuation header is synthesized so the consumer can pass the token even
        // when the spec doesn't declare it. When the spec DOES declare an x-continuation
        // header param it already lands in the headerParams loop below, so synthesizing it
        // again would emit a duplicate property (TS2300 / TS1117). Only synthesize when absent.
        var specHasContinuation = headerParams.Any(p =>
            string.Equals(p.Name, "x-continuation", StringComparison.OrdinalIgnoreCase));

        var headerProps = new List<string>();
        foreach (var p in headerParams)
        {
            var rawName = p.Name ?? string.Empty;
            var t = TypeScriptOperationHelper.GetParameterType(p, convertDates);
            var optional = p.Required ? string.Empty : "?";
            headerProps.Add("'" + rawName + "'" + optional + ": " + t);
        }

        if (!specHasContinuation)
        {
            headerProps.Add("'x-continuation'?: string");
        }

        paramParts.Add("headers?: { " + string.Join("; ", headerProps) + " }");

        var paramList = string.Join(", ", paramParts);
        sb.AppendLine();
        sb.Append("  async ").Append(pageMethodName).Append('(').Append(paramList).Append("): Promise<").Append(pageResultTypeName).AppendLine("> {");

        var interpolatedPath = TypeScriptOperationHelper.BuildInterpolatedPath(path, pathParams, namingStrategy, convertDates);
        var hasQuery = queryParams.Count > 0;

        sb.Append("    return this.api.request<").Append(pageDataType).Append(">('GET', ").Append(interpolatedPath).AppendLine(", {");

        if (hasQuery)
        {
            sb.AppendLine("      query: {");
            foreach (var p in queryParams)
            {
                var propName = (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
                if (!(p.Name ?? string.Empty).Equals(propName, StringComparison.Ordinal))
                {
                    sb.Append("        '").Append(p.Name).Append("': query?.").Append(propName).AppendLine(",");
                }
                else
                {
                    sb.Append("        ").Append(propName).Append(": query?.").Append(propName).AppendLine(",");
                }
            }

            sb.AppendLine("      },");
        }

        // Always emit the headers bag — the consumer hook always passes the continuation
        // header when paginating, even if the spec didn't declare any other headers.
        sb.AppendLine("      headers: {");
        foreach (var p in headerParams)
        {
            var rawName = p.Name ?? string.Empty;
            sb.Append("        '").Append(rawName).Append("': headers?.['").Append(rawName).AppendLine("'],");
        }

        // Mirror the signature: only synthesize the continuation entry when the spec
        // didn't already declare it (the loop above emits the real one in that case).
        if (!specHasContinuation)
        {
            sb.AppendLine("        'x-continuation': headers?.['x-continuation'],");
        }

        sb.AppendLine("      },");

        sb.Append("    })").Append(" as Promise<").Append(pageResultTypeName).AppendLine(">;");
        sb.AppendLine("  }");
    }

    private static void AppendStandardMethod(
        StringBuilder sb,
        string methodName,
        string path,
        string httpMethod,
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        List<OpenApiParameter> headerParams,
        IOpenApiSchema? bodySchema,
        string bodyContentType,
        bool isFileUpload,
        bool isFileDownload,
        bool isTextDownload,
        string returnType,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        string? perOpResultTypeName,
        HashSet<string> writableSchemas,
        bool brandedIds,
        ZodResponseSchemaSpec? zodSpec)
    {
        // Build parameter list
        var paramList = BuildParameterList(pathParams, queryParams, headerParams, bodySchema, bodyContentType, isFileUpload, namingStrategy, convertDates, writableSchemas, brandedIds, path);

        // perOpResultTypeName is supplied for every non-streaming op when called from
        // GenerateClientClass. The generic ApiResult fallback is a safety net for any unit
        // test that builds a method in isolation without first computing the per-op alias.
        var resultType = perOpResultTypeName ?? ("ApiResult<" + returnType + ">");
        sb.Append("  async ").Append(methodName).Append('(').Append(paramList).Append("): Promise<").Append(resultType).AppendLine("> {");

        // Build path with interpolation
        var interpolatedPath = TypeScriptOperationHelper.BuildInterpolatedPath(path, pathParams, namingStrategy, convertDates);

        // Build request options
        var hasQuery = queryParams.Count > 0;
        var hasHeaders = headerParams.Count > 0;
        var hasBody = bodySchema != null;

        // Cast suffix narrows the generic ApiResult<T> returned by ApiClient.request to the
        // per-op union — sound because handleResponse emits the same discriminator names.
        var castSuffix = perOpResultTypeName != null
            ? " as Promise<" + perOpResultTypeName + ">"
            : string.Empty;

        // The Zod schema only validates JSON bodies. File/text downloads carry no
        // structured payload, so skip emission even when the spec is non-null.
        var emitParseSchema = zodSpec != null && !isFileDownload && !isTextDownload;

        if (hasQuery || hasHeaders || hasBody || isFileUpload || isFileDownload || isTextDownload || emitParseSchema)
        {
            sb.Append("    return this.api.request<").Append(returnType).Append(">('").Append(httpMethod).Append("', ").Append(interpolatedPath).AppendLine(", {");

            if (hasBody && isFileUpload)
            {
                AppendFormDataBody(sb, bodySchema!, bodyContentType, namingStrategy);
            }
            else if (hasBody)
            {
                sb.AppendLine("      body,");
            }

            if (hasQuery)
            {
                AppendQueryObject(sb, queryParams, namingStrategy, convertDates);
            }

            if (hasHeaders)
            {
                AppendHeadersObject(sb, headerParams, convertDates);
            }

            if (isFileDownload)
            {
                sb.AppendLine("      responseType: 'blob',");
            }
            else if (isTextDownload)
            {
                sb.AppendLine("      responseType: 'text',");
            }

            if (emitParseSchema)
            {
                sb.Append("      parseSchema: ").Append(zodSpec!.Expression).AppendLine(",");
            }

            sb.Append("    })").Append(castSuffix).AppendLine(";");
        }
        else
        {
            sb.Append("    return this.api.request<").Append(returnType).Append(">('").Append(httpMethod).Append("', ").Append(interpolatedPath).Append(')').Append(castSuffix).AppendLine(";");
        }

        sb.AppendLine("  }");
    }

    private static void AppendStreamingMethod(
        StringBuilder sb,
        string methodName,
        string path,
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        List<OpenApiParameter> headerParams,
        string itemType,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        // Build parameter list (streaming methods may have query / header params + signal)
        var paramParts = new List<string>();

        foreach (var param in pathParams)
        {
            var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var paramType = TypeScriptOperationHelper.GetParameterType(param, convertDates, brandedIds, path);
            paramParts.Add(paramName + ": " + paramType);
        }

        if (queryParams.Count > 0)
        {
            var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(queryParams, namingStrategy, convertDates);
            paramParts.Add("query?: " + queryType);
        }

        if (headerParams.Count > 0)
        {
            var headerType = TypeScriptOperationHelper.BuildHeaderTypeInline(headerParams, convertDates);
            paramParts.Add("headers?: " + headerType);
        }

        paramParts.Add("signal?: AbortSignal");

        var paramList = string.Join(", ", paramParts);

        sb.Append("  async *").Append(methodName).Append('(').Append(paramList).Append("): AsyncGenerator<").Append(itemType).AppendLine("> {");

        var interpolatedPath = TypeScriptOperationHelper.BuildInterpolatedPath(path, pathParams, namingStrategy, convertDates);
        var hasQuery = queryParams.Count > 0;
        var hasHeaders = headerParams.Count > 0;

        if (hasQuery || hasHeaders)
        {
            sb.Append("    yield* this.api.requestStream<").Append(itemType).Append(">('GET', ").Append(interpolatedPath).AppendLine(", {");
            if (hasQuery)
            {
                AppendQueryObject(sb, queryParams, namingStrategy, convertDates);
            }

            if (hasHeaders)
            {
                AppendHeadersObject(sb, headerParams, convertDates);
            }

            sb.AppendLine("      signal,");
            sb.AppendLine("    });");
        }
        else
        {
            sb.Append("    yield* this.api.requestStream<").Append(itemType).Append(">('GET', ").Append(interpolatedPath).AppendLine(", { signal });");
        }

        sb.AppendLine("  }");
    }

    private static string BuildParameterList(
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        List<OpenApiParameter> headerParams,
        IOpenApiSchema? bodySchema,
        string bodyContentType,
        bool isFileUpload,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        HashSet<string> writableSchemas,
        bool brandedIds,
        string path)
    {
        var parts = new List<string>();

        // Path parameters (required)
        foreach (var param in pathParams)
        {
            var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var paramType = TypeScriptOperationHelper.GetParameterType(param, convertDates, brandedIds, path);
            parts.Add(paramName + ": " + paramType);
        }

        // Request body
        if (bodySchema != null)
        {
            if (isFileUpload)
            {
                AppendFileUploadParams(parts, bodySchema, bodyContentType, namingStrategy);
            }
            else
            {
                // When the body schema is a direct $ref to a schema with the readOnly/writeOnly
                // split, route the parameter to the Writable variant — that's the type with the
                // readOnly properties dropped, matching the OpenAPI contract for request bodies.
                var bodyType = bodySchema.ToTypeScriptReturnType();
                if (bodySchema is OpenApiSchemaReference bodyRef)
                {
                    var refName = bodyRef.Reference.Id ?? bodyRef.Id;
                    if (refName != null && writableSchemas.Contains(refName))
                    {
                        bodyType = refName + TypeScriptModelExtractor.WritableSuffix;
                    }
                }

                parts.Add("body: " + bodyType);
            }
        }

        // Query parameters (optional object)
        if (queryParams.Count > 0)
        {
            var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(queryParams, namingStrategy, convertDates);
            parts.Add("query?: " + queryType);
        }

        // Header parameters (optional object)
        if (headerParams.Count > 0)
        {
            var headerType = TypeScriptOperationHelper.BuildHeaderTypeInline(headerParams, convertDates);
            parts.Add("headers?: " + headerType);
        }

        return string.Join(", ", parts);
    }

    private static void AppendFileUploadParams(
        List<string> parts,
        IOpenApiSchema bodySchema,
        string bodyContentType,
        TypeScriptNamingStrategy namingStrategy)
    {
        // For raw binary upload (application/octet-stream), single file parameter
        if (bodyContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("file: Blob | File");
            return;
        }

        // For multipart/form-data with array of files
        if (bodySchema is OpenApiSchema { Type: var type } && type?.HasFlag(JsonSchemaType.Array) == true)
        {
            parts.Add("files: (Blob | File)[]");
            return;
        }

        // For multipart/form-data with object schema (has properties)
        if (bodySchema.Properties is { Count: > 0 })
        {
            var formParts = new List<string>();
            var required = new HashSet<string>(StringComparer.Ordinal);
            if (bodySchema is OpenApiSchema actualSchema && actualSchema.Required != null)
            {
                foreach (var r in actualSchema.Required)
                {
                    required.Add(r);
                }
            }

            foreach (var prop in bodySchema.Properties)
            {
                var propName = prop.Key.ApplyNamingStrategy(namingStrategy);
                var isRequired = required.Contains(prop.Key);
                var propType = prop.Value.ToTypeScriptTypeForModel(isRequired);

                // File properties: binary -> Blob | File
                if (prop.Value is OpenApiSchema propSchema)
                {
                    if (propSchema.Type?.HasFlag(JsonSchemaType.String) == true &&
                        string.Equals(propSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
                    {
                        propType = "Blob | File";
                    }
                    else if (propSchema.Type?.HasFlag(JsonSchemaType.Array) == true &&
                             propSchema.Items is OpenApiSchema itemSchema &&
                             itemSchema.Type?.HasFlag(JsonSchemaType.String) == true &&
                             string.Equals(itemSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
                    {
                        propType = "(Blob | File)[]";
                    }
                }

                var optional = isRequired ? string.Empty : "?";
                formParts.Add(propName + optional + ": " + propType);
            }

            parts.Add("data: { " + string.Join("; ", formParts) + " }");
            return;
        }

        // Fallback: generic FormData
        parts.Add("data: FormData");
    }

    private static void AppendFormDataBody(
        StringBuilder sb,
        IOpenApiSchema bodySchema,
        string bodyContentType,
        TypeScriptNamingStrategy namingStrategy)
    {
        // For raw binary upload (application/octet-stream), pass file directly
        if (bodyContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("      body: file,");
            return;
        }

        sb.AppendLine("      body: (() => {");
        sb.AppendLine("        const formData = new FormData();");

        // For array of files
        if (bodySchema is OpenApiSchema { Type: var type } && type?.HasFlag(JsonSchemaType.Array) == true)
        {
            sb.AppendLine("        for (const file of files) {");
            sb.AppendLine("          formData.append('files', file);");
            sb.AppendLine("        }");
            sb.AppendLine("        return formData;");
            sb.AppendLine("      })(),");
            return;
        }

        // For object schema with properties
        if (bodySchema.Properties is { Count: > 0 })
        {
            foreach (var prop in bodySchema.Properties)
            {
                var propName = prop.Key.ApplyNamingStrategy(namingStrategy);
                var isFileProperty = false;
                var isFileArrayProperty = false;

                if (prop.Value is OpenApiSchema propSchema)
                {
                    if (propSchema.Type?.HasFlag(JsonSchemaType.String) == true &&
                        string.Equals(propSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
                    {
                        isFileProperty = true;
                    }
                    else if (propSchema.Type?.HasFlag(JsonSchemaType.Array) == true &&
                             propSchema.Items is OpenApiSchema itemSchema &&
                             itemSchema.Type?.HasFlag(JsonSchemaType.String) == true &&
                             string.Equals(itemSchema.Format, "binary", StringComparison.OrdinalIgnoreCase))
                    {
                        isFileArrayProperty = true;
                    }
                }

                if (isFileProperty)
                {
                    sb.Append("        if (data.").Append(propName).Append(" != null) formData.append('").Append(prop.Key).Append("', data.").Append(propName).AppendLine(");");
                }
                else if (isFileArrayProperty)
                {
                    sb.Append("        for (const f of data.").Append(propName).Append(" ?? []) formData.append('").Append(prop.Key).AppendLine("', f);");
                }
                else if (prop.Value is OpenApiSchema ps && ps.Type?.HasFlag(JsonSchemaType.Array) == true)
                {
                    sb.Append("        for (const item of data.").Append(propName).Append(" ?? []) formData.append('").Append(prop.Key).AppendLine("', String(item));");
                }
                else
                {
                    sb.Append("        if (data.").Append(propName).Append(" != null) formData.append('").Append(prop.Key).Append("', String(data.").Append(propName).AppendLine("));");
                }
            }

            sb.AppendLine("        return formData;");
            sb.AppendLine("      })(),");
            return;
        }

        // Fallback
        sb.AppendLine("        return data;");
        sb.AppendLine("      })(),");
    }

    private static void AppendQueryObject(
        StringBuilder sb,
        List<OpenApiParameter> queryParams,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates)
    {
        sb.AppendLine("      query: {");
        foreach (var param in queryParams)
        {
            var propName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var coercion = convertDates && TypeScriptOperationHelper.IsDateParam(param)
                ? "?" + TypeScriptOperationHelper.GetDateSerializationSuffix(param)
                : string.Empty;

            // Use original name as key if different from the transformed name
            if (!(param.Name ?? string.Empty).Equals(propName, StringComparison.Ordinal))
            {
                sb.Append("        '").Append(param.Name).Append("': query?.").Append(propName).Append(coercion).AppendLine(",");
            }
            else
            {
                sb.Append("        ").Append(propName).Append(": query?.").Append(propName).Append(coercion).AppendLine(",");
            }
        }

        sb.AppendLine("      },");
    }

    private static void AppendHeadersObject(
        StringBuilder sb,
        List<OpenApiParameter> headerParams,
        bool convertDates)
    {
        // Header names are emitted verbatim (no naming-strategy transform). HTTP header
        // names commonly contain dashes — both the key on the wire and the inline-type
        // key must match the OpenAPI name exactly. Access uses bracket + optional-chaining
        // because the outer `headers?:` is optional.
        sb.AppendLine("      headers: {");
        foreach (var param in headerParams)
        {
            var rawName = param.Name ?? string.Empty;
            var coercion = convertDates && TypeScriptOperationHelper.IsDateParam(param)
                ? "?" + TypeScriptOperationHelper.GetDateSerializationSuffix(param)
                : string.Empty;
            sb.Append("        '").Append(rawName).Append("': headers?.['").Append(rawName).Append("']").Append(coercion).AppendLine(",");
        }

        sb.AppendLine("      },");
    }
}