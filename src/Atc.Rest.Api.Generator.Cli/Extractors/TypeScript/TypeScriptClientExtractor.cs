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
        bool zodRuntimeValidate = false,
        TypeScriptWireNames? wireNames = null)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        wireNames ??= new TypeScriptWireNames(openApiDoc, namingStrategy, convertDates);
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
            var content = GenerateClientClass(className, operations, openApiDoc, headerContent, enumNames, namingStrategy, convertDates, httpClient, writableSchemas, brandedIds, zodRuntimeValidate, wireNames);
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
        bool zodRuntimeValidate,
        TypeScriptWireNames wireNames)
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
                if (refName is not null && writableSchemas.Contains(refName))
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
                    if (wrapperName is not null)
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
                        if (itemName is not null)
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
        var perOpResultTypes = new Dictionary<OpenApiOperation, string>(ReferenceEqualityComparer.Instance);
        var perOpArms = new Dictionary<OpenApiOperation, string>(ReferenceEqualityComparer.Instance);
        var perOpDeclarations = new List<string>();
        var perOpErrorImports = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (operationPath, methodVerb, operation) in operations)
        {
            if (operation.IsStreamingResponse())
            {
                // Pure streaming ops return AsyncGenerator<Item> directly — no result alias.
                continue;
            }

            // The errors the server produces without the spec listing them (the same rules as its
            // endpoint metadata), so the per-op union covers every arm handleResponse can return.
            var features = openApiDoc.Paths is not null && openApiDoc.Paths.TryGetValue(operationPath, out var pathItemForFeatures)
                ? OperationFeaturesHelper.DetectOperationFeatures(operation, pathItemForFeatures, openApiDoc, methodVerb.ToUpperInvariant())
                : null;

            var isFileDownload = operation.HasFileDownload();
            var isTextDownload = !isFileDownload && operation.HasTextResponse();
            var operationId = operation.GetOperationId(operationPath, methodVerb);
            var methodName = operationId.ToCamelCase().ToTypeScriptIdentifier();

            var resultTypeName = methodName.ToPascalCase() + "Result";

            var (declaration, imports, arms) = TypeScriptOperationHelper.BuildPerOperationResultType(
                operation,
                resultTypeName,
                isFileDownload,
                isTextDownload,
                httpClient,
                features);

            perOpResultTypes[operation] = resultTypeName;
            perOpArms[operation] = "[" + string.Join(", ", arms.Select(a => "'" + a + "'")) + "]";
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
                // Mirror the emission condition in AppendStandardMethod: streams and file / text
                // downloads carry no parseSchema, so importing a schema for them leaves it unused.
                if (operation.IsStreamingResponse() ||
                    operation.HasFileDownload() ||
                    operation.HasTextResponse())
                {
                    continue;
                }

                var spec = TypeScriptOperationHelper.TryGetResponseZodSchemaSpec(operation);
                if (spec is null)
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

        // Emit per-operation result-type aliases before the class. A spec model named `Error` imported into this
        // file shadows the global Error that the parseError arm carries: name the JS one explicitly then.
        var modelShadowsError = importTypes.Contains("Error");
        foreach (var declaration in perOpDeclarations)
        {
            bodySb.Append(modelShadowsError
                ? declaration.Replace("status: 'parseError'; error: Error;", "status: 'parseError'; error: globalThis.Error;", StringComparison.Ordinal)
                : declaration);
            bodySb.AppendLine();
        }

        // Class declaration
        bodySb.Append("export class ").Append(className).AppendLine(" {");
        bodySb.AppendLine("  private readonly api: ApiClient;");
        bodySb.AppendLine();
        bodySb.AppendLine("  constructor(api: ApiClient) {");
        bodySb.AppendLine("    this.api = api;");
        bodySb.AppendLine("  }");

        // Generate methods (the helpers/xml.ts functions the operations with an XML body use are collected)
        var xmlFunctions = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (path, method, operation) in operations)
        {
            bodySb.AppendLine();
            perOpResultTypes.TryGetValue(operation, out var resultTypeName);
            perOpArms.TryGetValue(operation, out var armsOption);
            AppendMethod(bodySb, path, method, operation, openApiDoc, namingStrategy, convertDates, resultTypeName, armsOption, writableSchemas, brandedIds, zodRuntimeValidate, wireNames, xmlFunctions);
        }

        bodySb.AppendLine("}");

        var bodyText = bodySb.ToString();

        // Write header
        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        // Write imports (narrowed to what the body references)
        AppendImports(sb, importTypes, enumNames, perOpErrorImports, httpClient, brandImports, zodSchemaImports, needsZodImport, bodyText);
        if (xmlFunctions.Count > 0)
        {
            sb.Append("import { ").Append(string.Join(", ", xmlFunctions)).AppendLine(" } from '../helpers/xml';");
        }

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

                var brand = TypeScriptBrandedIdExtractor.ResolveParamBrand(path, param.Name, param.Schema);
                if (brand is not null)
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

            if (enumNames is not null && enumNames.Contains(typeName))
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

        // Operations with their own retry policy pass it as a request option.
        if (bodyText.Contains("retryPolicies.", StringComparison.Ordinal))
        {
            sb.AppendLine("import { retryPolicies } from '../helpers/retryConfig';");
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

    /// <summary>
    /// The <c>xml</c> request option of an operation with an XML body: the read function of an XML (or JSON+XML)
    /// success response with the Accept to send, and the write function of an XML-only request body.
    /// </summary>
    private static string? BuildXmlOption(
        OpenApiOperation operation,
        ISet<string>? xmlFunctions)
    {
        var parts = new List<string>();
        var success = operation.Responses?
            .Where(r => r.Key.Length == 3 && r.Key[0] == '2')
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => r.Value)
            .FirstOrDefault();
        var content = success?.Content;
        if ((XmlBodyHelper.FindXmlOnlyBody(content) ?? XmlBodyHelper.FindXmlAlternative(content)) is { } responseXml &&
            ModelKey(content![responseXml.MediaType].Schema, responseXml.IsList) is { } responseKey)
        {
            var names = TypeScriptXmlHelperExtractor.FunctionNames(responseKey);
            var read = responseXml.IsList ? names.ReadList : names.Read;
            xmlFunctions?.Add(read);
            parts.Add("read: " + read);
            parts.Add("accept: '" + XmlBodyHelper.AcceptFor([responseXml], XmlBodyHelper.ErrorMediaTypes(operation), preferXml: false, operation.GetModelOrFileMediaTypes()) + "'");
        }

        var requestContent = operation.RequestBody?.Content;
        if (XmlBodyHelper.FindXmlOnlyBody(requestContent) is { } requestXml &&
            ModelKey(requestContent![requestXml.MediaType].Schema, requestXml.IsList) is { } requestKey)
        {
            var names = TypeScriptXmlHelperExtractor.FunctionNames(requestKey);
            var write = requestXml.IsList ? names.WriteList : names.Write;
            xmlFunctions?.Add(write);
            parts.Add("write: " + (requestXml.IsList ? write : "(body: never) => " + write + "(body)"));
            parts.Add("contentType: '" + requestXml.MediaType + "; charset=utf-8'");
        }

        // Declared XML error models: read into the model, then handled like a JSON error body
        var errorReaders = new List<string>();
        foreach (var (statusCode, response) in operation.Responses ?? new OpenApiResponses())
        {
            if (!ErrorResponseShapeResolver.IsErrorStatusCode(statusCode) ||
                ErrorResponseShapeResolver.Resolve(response) is not { Kind: ErrorResponseKind.XmlObject, Schema: { } errorSchema } ||
                ModelKey(errorSchema, isList: false) is not { } errorKey)
            {
                continue;
            }

            var read = TypeScriptXmlHelperExtractor.FunctionNames(errorKey).Read;
            xmlFunctions?.Add(read);
            errorReaders.Add("'" + statusCode + "': " + read);
        }

        if (errorReaders.Count > 0)
        {
            parts.Add("errors: { " + string.Join(", ", errorReaders) + " }");
        }

        return parts.Count == 0 ? null : "{ " + string.Join(", ", parts) + " }";

        static string? ModelKey(
            IOpenApiSchema? schema,
            bool isList)
        {
            var reference = isList ? (schema as OpenApiSchema)?.Items as OpenApiSchemaReference : schema as OpenApiSchemaReference;
            return reference?.Reference?.Id ?? reference?.Id;
        }
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
        string? armsOption,
        HashSet<string> writableSchemas,
        bool brandedIds,
        bool zodRuntimeValidate,
        TypeScriptWireNames wireNames,
        ISet<string>? xmlFunctions = null)
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
        var cookieParams = TypeScriptOperationHelper.GetMergedParameters(operation, openApiDoc, path, ParameterLocation.Cookie);
        var querystringParams = TypeScriptOperationHelper.GetMergedParameters(operation, openApiDoc, path, ParameterLocation.QueryString);

        // Get request body
        var (bodySchema, bodyContentType) = operation.GetRequestBodySchemaWithContentType();

        // Get response type
        var returnType = TypeScriptOperationHelper.GetReturnType(operation, isStreaming, isFileDownload);

        // Per-operation retry (x-retry-*): only emitted when the operation differs from what the ApiClient
        // does on its own (its default policy for safe methods, no retry for POST/PATCH).
        var retryOption = BuildRetryOption(operation, path, httpMethod, openApiDoc);

        // How to rename the body's, the response's and the error bodies' keys between the wire and the models
        // (a form's fields are written under their wire names already).
        var wireOption = wireNames.OperationOption(operation, isStreaming, isFileUpload || OpenApiOperationExtensions.IsFormContentType(bodyContentType), isFileDownload || isTextDownload);

        // Emit a /** ... */ block when the spec has anything worth saying
        // about this operation — summary, description, or deprecated marker. The same
        // block applies to the Page companion below (it shares the operation).
        AppendOperationJsDoc(sb, operation);

        if (isStreaming)
        {
            // Map the wire framing to the requestStream framing argument. SSE maps to sse;
            // JSON Lines maps to json-lines and JSON Text Sequence maps to json-seq (both resolve
            // to the runtime brace-scan default path, but threading the arg keeps the call site
            // explicit); multipart/mixed maps to multipart (its own runtime parse branch).
            // JsonArray omits the arg to keep its call site unchanged (the default is json-array).
            var streamFramingArg = operation.GetStreamingFraming() switch
            {
                StreamingFraming.ServerSentEvents => "'sse'",
                StreamingFraming.JsonLines => "'json-lines'",
                StreamingFraming.JsonSequence => "'json-seq'",
                StreamingFraming.MultipartMixed => "'multipart'",
                _ => null,
            };

            var streamBodyType = !isFileUpload && bodySchema is not null ? BodyTypeOf(bodySchema, writableSchemas) : null;
            AppendStreamingMethod(sb, methodName, path, httpMethod, pathParams, queryParams, headerParams, returnType, streamBodyType, bodyContentType, namingStrategy, convertDates, brandedIds, streamFramingArg, wireOption);
        }
        else
        {
            // Compute the Zod schema spec for this op's response (if any). Threaded
            // through so AppendStandardMethod can splice `parseSchema: <expr>` into
            // the request options bag.
            var zodSpec = zodRuntimeValidate
                ? TypeScriptOperationHelper.TryGetResponseZodSchemaSpec(operation)
                : null;

            // A model or a file: read by the answer's Content-Type, asking for the model's and the file's media types
            var modelOrFileAccept = operation.GetModelOrFileMediaTypes() is { Count: > 0 } fileMediaTypes
                ? XmlBodyHelper.AcceptFor(XmlBodyHelper.FindXmlOnlyBody(operation.Responses?["200"]?.Content) is { } xmlOnly ? [xmlOnly] : [], XmlBodyHelper.ErrorMediaTypes(operation), preferXml: false, fileMediaTypes)
                : null;

            // A raw file upload: the media types the request body declares
            var rawUploadMediaTypes = operation.RequestBody?.Content?.Keys.Where(OpenApiOperationExtensions.IsRawFileUploadContentType).ToList();
            AppendStandardMethod(sb, methodName, path, httpMethod, pathParams, queryParams, headerParams, cookieParams, querystringParams, bodySchema, bodyContentType, isFileUpload, isFileDownload, isTextDownload, returnType, namingStrategy, convertDates, perOpResultTypeName, writableSchemas, brandedIds, zodSpec, retryOption, armsOption, wireOption, BuildXmlOption(operation, xmlFunctions), modelOrFileAccept, rawUploadMediaTypes);
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
    /// The <c>retry</c> request option for an operation (e.g. <c>{ policy: retryPolicies.ordersFast }</c>
    /// or <c>{ enabled: false, timeoutMs: 600000 }</c>), or null when the ApiClient's own behavior already
    /// matches: its default policy for safe methods, no retry for POST/PATCH. Uses the same resolution as
    /// the C# client (<see cref="OpenApiRetryExtensions.ResolveOperationRetry"/>).
    /// </summary>
    private static string? BuildRetryOption(
        OpenApiOperation operation,
        string path,
        string httpMethod,
        OpenApiDocument openApiDoc)
    {
        if (!openApiDoc.HasRetryConfiguration() ||
            openApiDoc.Paths is null ||
            !openApiDoc.Paths.TryGetValue(path, out var pathItem))
        {
            return null;
        }

        var retry = operation.ResolveOperationRetry(pathItem, openApiDoc, httpMethod);
        if (retry.IsClientDefault(httpMethod))
        {
            return null;
        }

        var parts = new List<string>();
        if (retry.PolicyName is not null)
        {
            parts.Add("policy: retryPolicies." + TypeScriptRetryConfigExtractor.ToCamelCase(retry.PolicyName));
        }

        if (retry.Mode == OperationRetryMode.NoRetry)
        {
            parts.Add("enabled: false");
        }
        else if (OpenApiRetryExtensions.IsRetryUnsafeHttpMethod(httpMethod))
        {
            // POST/PATCH are not retried unless told to; the operation opted in.
            parts.Add("enabled: true");
        }

        if (retry.TimeoutSeconds is { } timeoutSeconds)
        {
            parts.Add("timeoutMs: " + (timeoutSeconds * 1000).ToString("G", CultureInfo.InvariantCulture));
        }

        return "{ " + string.Join(", ", parts) + " }";
    }

    private static void AppendStandardMethod(
        StringBuilder sb,
        string methodName,
        string path,
        string httpMethod,
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        List<OpenApiParameter> headerParams,
        List<OpenApiParameter> cookieParams,
        List<OpenApiParameter> querystringParams,
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
        ZodResponseSchemaSpec? zodSpec,
        string? retryOption,
        string? armsOption = null,
        string? wireOption = null,
        string? xmlOption = null,
        string? modelOrFileAccept = null,
        IReadOnlyList<string>? rawUploadMediaTypes = null)
    {
        // Build parameter list
        var (paramList, signalName) = BuildParameterList(pathParams, queryParams, headerParams, cookieParams, querystringParams, bodySchema, bodyContentType, isFileUpload, namingStrategy, convertDates, writableSchemas, brandedIds, path);

        // perOpResultTypeName is supplied for every non-streaming op when called from
        // GenerateClientClass. The generic ApiResult fallback is a safety net for any unit
        // test that builds a method in isolation without first computing the per-op alias.
        var resultType = perOpResultTypeName ?? ("ApiResult<" + returnType + ">");
        sb.Append("  async ").Append(methodName).Append('(').Append(paramList).Append("): Promise<").Append(resultType).AppendLine("> {");

        // Build path with interpolation
        var interpolatedPath = TypeScriptOperationHelper.BuildInterpolatedPath(path, pathParams, namingStrategy, convertDates);

        // OAS 3.2 in:querystring: emit _basePath/_url locals before the return statement.
        // The raw querystring value is appended verbatim so callers can pass pre-encoded
        // query strings (e.g. "q=hello&sort=asc") without double-encoding.
        var hasQuerystring = querystringParams.Count > 0;
        if (hasQuerystring)
        {
            sb.Append("    const _basePath = ").Append(interpolatedPath).AppendLine(";");
            if (querystringParams.Count == 1)
            {
                var qsName = TypeScriptOperationHelper.ParameterIdentifier(querystringParams[0], namingStrategy);
                sb.Append("    const _url = ").Append(qsName).Append(" ? `${_basePath}?${").Append(qsName).AppendLine("}` : _basePath;");
            }
            else
            {
                sb.AppendLine("    const _rawParts = [");
                foreach (var qsParam in querystringParams)
                {
                    var qsName = TypeScriptOperationHelper.ParameterIdentifier(qsParam, namingStrategy);
                    sb.Append("      ").Append(qsName).AppendLine(",");
                }

                sb.AppendLine("    ].filter(Boolean).join('&');");
                sb.AppendLine("    const _url = _rawParts ? `${_basePath}?${_rawParts}` : _basePath;");
            }
        }

        // Build request options
        var hasQuery = queryParams.Count > 0;
        var hasHeaders = headerParams.Count > 0 || cookieParams.Count > 0;
        var hasBody = bodySchema is not null;

        // A URL-encoded form: the model's fields in a URLSearchParams (a multipart form is a file upload)
        var isUrlEncodedForm = hasBody && !isFileUpload && OpenApiOperationExtensions.IsFormContentType(bodyContentType);

        // Cast suffix narrows the generic ApiResult<T> returned by ApiClient.request to the
        // per-op union — sound because handleResponse emits the same discriminator names.
        var castSuffix = perOpResultTypeName is not null
            ? " as Promise<" + perOpResultTypeName + ">"
            : string.Empty;

        // The Zod schema only validates JSON bodies. File/text downloads carry no
        // structured payload, so skip emission even when the spec is non-null.
        var emitParseSchema = zodSpec is not null && !isFileDownload && !isTextDownload;

        var urlArg = hasQuerystring ? "_url" : interpolatedPath;

        // Every request carries the caller's signal, so the options bag is always there
        sb.Append("    return this.api.request<").Append(returnType).Append(">('").Append(httpMethod).Append("', ").Append(urlArg).AppendLine(", {");

        if (hasBody && isFileUpload)
        {
            AppendFormDataBody(sb, bodySchema!, bodyContentType, namingStrategy, rawUploadMediaTypes);
        }
        else if (hasBody && isUrlEncodedForm)
        {
            AppendUrlEncodedBody(sb, bodySchema!, namingStrategy, convertDates);
        }
        else if (hasBody)
        {
            sb.AppendLine("      body,");
            AppendJsonContentType(sb, bodyContentType);
        }

        if (hasQuery)
        {
            AppendQueryObject(sb, queryParams, namingStrategy, convertDates);
        }

        if (hasHeaders)
        {
            AppendHeadersObject(sb, headerParams, cookieParams, convertDates, namingStrategy, modelOrFileAccept);
        }
        else if (modelOrFileAccept is not null)
        {
            sb.Append("      headers: { Accept: '").Append(modelOrFileAccept).AppendLine("' },");
        }

        if (isFileDownload)
        {
            sb.AppendLine("      responseType: 'blob',");
        }
        else if (modelOrFileAccept is not null)
        {
            // A model or a file: the file is a Blob, the model is read as the model
            sb.AppendLine("      modelOrFile: true,");
        }
        else if (isTextDownload)
        {
            sb.AppendLine("      responseType: 'text',");
        }
        else if (!string.Equals(returnType, "void", StringComparison.Ordinal) &&
                 xmlOption?.Contains("read: ", StringComparison.Ordinal) != true)
        {
            // A declared JSON model: the body is read as JSON whatever the answer's Content-Type says (an empty
            // body is no data). An operation with an XML model keeps reading by Content-Type (helpers/xml.ts).
            sb.AppendLine("      responseType: 'json',");
        }

        if (emitParseSchema)
        {
            sb.Append("      parseSchema: ").Append(zodSpec!.Expression).AppendLine(",");
        }

        if (retryOption is not null)
        {
            sb.Append("      retry: ").Append(retryOption).AppendLine(",");
        }

        if (wireOption is not null)
        {
            sb.Append("      wire: ").Append(wireOption).AppendLine(",");
        }

        // An XML body (helpers/xml.ts): how to read the response and write the request
        if (xmlOption is not null)
        {
            sb.Append("      xml: ").Append(xmlOption).AppendLine(",");
        }

        // The result arms of the per-op union: a status it does not declare is folded into one it does.
        if (armsOption is not null)
        {
            sb.Append("      arms: ").Append(armsOption).AppendLine(",");
        }

        sb.AppendLine(signalName == "signal" ? "      signal," : "      signal: " + signalName + ",");
        sb.Append("    })").Append(castSuffix).AppendLine(";");

        sb.AppendLine("  }");
    }

    private static void AppendStreamingMethod(
        StringBuilder sb,
        string methodName,
        string path,
        string httpMethod,
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        List<OpenApiParameter> headerParams,
        string itemType,
        string? bodyType,
        string bodyContentType,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds,
        string? streamFramingArg = null,
        string? wireOption = null)
    {
        // Build parameter list (streaming methods may have query / header params + signal)
        var paramParts = new List<string>();

        foreach (var param in pathParams)
        {
            var paramName = TypeScriptOperationHelper.ParameterIdentifier(param, namingStrategy);
            var paramType = TypeScriptOperationHelper.GetParameterType(param, convertDates, brandedIds, path);
            paramParts.Add(paramName + ": " + paramType);
        }

        // A streamed search (POST or QUERY) sends its criteria as the body.
        if (bodyType is not null)
        {
            paramParts.Add("body: " + bodyType);
        }

        if (queryParams.Count > 0)
        {
            paramParts.Add(TypeScriptOperationHelper.BuildQueryParameter(queryParams, namingStrategy, convertDates));
        }

        if (headerParams.Count > 0)
        {
            paramParts.Add(TypeScriptOperationHelper.BuildHeaderParameter(headerParams, convertDates));
        }

        var signalName = TypeScriptOperationHelper.SignalParameterName(paramParts);
        var signalOption = signalName == "signal" ? "signal" : "signal: " + signalName;
        paramParts.Add(signalName + "?: AbortSignal");

        var paramList = TypeScriptOperationHelper.JoinParameters(paramParts);

        sb.Append("  async *").Append(methodName).Append('(').Append(paramList).Append("): AsyncGenerator<").Append(itemType).AppendLine("> {");

        var interpolatedPath = TypeScriptOperationHelper.BuildInterpolatedPath(path, pathParams, namingStrategy, convertDates);
        var hasQuery = queryParams.Count > 0;
        var hasHeaders = headerParams.Count > 0;

        var hasBody = bodyType is not null;
        var method = "'" + httpMethod.ToUpperInvariant() + "'";
        if (hasQuery || hasHeaders || hasBody)
        {
            sb.Append("    yield* this.api.requestStream<").Append(itemType).Append(">(").Append(method).Append(", ").Append(interpolatedPath).AppendLine(", {");
            if (hasBody)
            {
                sb.AppendLine("      body,");
                AppendJsonContentType(sb, bodyContentType);
            }

            if (hasQuery)
            {
                AppendQueryObject(sb, queryParams, namingStrategy, convertDates);
            }

            if (hasHeaders)
            {
                AppendHeadersObject(sb, headerParams, cookieParams: null, convertDates, namingStrategy);
            }

            sb.Append("      ").Append(signalOption).AppendLine(",");
            if (wireOption is not null)
            {
                sb.Append("      wire: ").Append(wireOption).AppendLine(",");
            }

            sb.AppendLine(streamFramingArg is null
                ? "    });"
                : $"    }}, {streamFramingArg});");
        }
        else
        {
            var options = wireOption is null ? "{ " + signalOption + " }" : "{ " + signalOption + ", wire: " + wireOption + " }";
            sb.Append("    yield* this.api.requestStream<").Append(itemType).Append(">(").Append(method).Append(", ").Append(interpolatedPath);
            sb.AppendLine(streamFramingArg is null
                ? $", {options});"
                : $", {options}, {streamFramingArg});");
        }

        sb.AppendLine("  }");
    }

    private static (string ParameterList, string SignalName) BuildParameterList(
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        List<OpenApiParameter> headerParams,
        List<OpenApiParameter> cookieParams,
        List<OpenApiParameter> querystringParams,
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
            var paramName = TypeScriptOperationHelper.ParameterIdentifier(param, namingStrategy);
            var paramType = TypeScriptOperationHelper.GetParameterType(param, convertDates, brandedIds, path);
            parts.Add(paramName + ": " + paramType);
        }

        // Request body
        if (bodySchema is not null)
        {
            if (isFileUpload)
            {
                AppendFileUploadParams(parts, bodySchema, bodyContentType, namingStrategy);
            }
            else
            {
                parts.Add("body: " + BodyTypeOf(bodySchema, writableSchemas));
            }
        }

        // OAS 3.2 in:querystring parameters — individual optional string args placed before
        // the query? object so callers can pass the raw query string without wrapping it.
        foreach (var param in querystringParams)
        {
            var paramName = TypeScriptOperationHelper.ParameterIdentifier(param, namingStrategy);
            parts.Add(paramName + "?: string");
        }

        // Query parameters (optional object)
        if (queryParams.Count > 0)
        {
            parts.Add(TypeScriptOperationHelper.BuildQueryParameter(queryParams, namingStrategy, convertDates));
        }

        // Header parameters (optional object)
        if (headerParams.Count > 0)
        {
            parts.Add(TypeScriptOperationHelper.BuildHeaderParameter(headerParams, convertDates));
        }

        // OAS 3.2 in:cookie parameters — grouped cookies? object placed after headers?.
        if (cookieParams.Count > 0)
        {
            var cookieType = BuildCookieTypeInline(cookieParams, namingStrategy);
            parts.Add("cookies?: " + cookieType);
        }

        // The caller's cancellation (React Query hands its own): last, so every argument before it keeps its place
        var signalName = TypeScriptOperationHelper.SignalParameterName(parts);
        parts.Add(signalName + "?: AbortSignal");

        return (TypeScriptOperationHelper.JoinParameters(parts), signalName);
    }

    /// <summary>
    /// The type of a JSON request body parameter. When the body schema is a direct $ref to a schema with the
    /// readOnly/writeOnly split, it is the Writable variant - the type with the readOnly properties dropped,
    /// matching the OpenAPI contract for request bodies.
    /// </summary>
    private static string BodyTypeOf(
        IOpenApiSchema bodySchema,
        HashSet<string> writableSchemas)
    {
        if (bodySchema is OpenApiSchemaReference bodyRef &&
            (bodyRef.Reference.Id ?? bodyRef.Id) is { } refName &&
            writableSchemas.Contains(refName))
        {
            return refName + TypeScriptModelExtractor.WritableSuffix;
        }

        return bodySchema.ToTypeScriptReturnType();
    }

    /// <summary>
    /// A JSON body under another JSON media type (application/merge-patch+json, application/vnd.x+json) is sent
    /// with that Content-Type.
    /// </summary>
    private static void AppendJsonContentType(
        StringBuilder sb,
        string mediaType)
    {
        if (mediaType.Split(';')[0].Trim().EndsWith("+json", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("      contentType: '").Append(mediaType).AppendLine("',");
        }
    }

    private static string BuildCookieTypeInline(
        List<OpenApiParameter> cookieParams,
        TypeScriptNamingStrategy namingStrategy)
    {
        var props = cookieParams.Select(p =>
        {
            var name = TypeScriptOperationHelper.PropertyKey((p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
            var optional = p.Required ? string.Empty : "?";
            return name + optional + ": " + TypeScriptOperationHelper.GetParameterType(p, convertDates: false);
        });
        return "{ " + string.Join("; ", props) + " }";
    }

    private static void AppendFileUploadParams(
        List<string> parts,
        IOpenApiSchema bodySchema,
        string bodyContentType,
        TypeScriptNamingStrategy namingStrategy)
    {
        // For raw binary upload (application/octet-stream), single file parameter
        if (OpenApiOperationExtensions.IsRawFileUploadContentType(bodyContentType))
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
            if (bodySchema is OpenApiSchema { Required: not null } actualSchema)
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

                // A form part is left out when it has no value, never sent as null
                var propType = prop.Value.ToTypeScriptTypeForModelWithoutOptionalNull(isRequired);

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
        TypeScriptNamingStrategy namingStrategy,
        IReadOnlyList<string>? rawUploadMediaTypes = null)
    {
        // A raw binary upload: the file, sent as the declared media type (a server that accepts only it answers any
        // other with 415). One concrete media type is sent as it; a wildcard or several send the file's own type when
        // the declaration covers it, else the first concrete one - never a literal wildcard.
        if (OpenApiOperationExtensions.IsRawFileUploadContentType(bodyContentType))
        {
            var declared = rawUploadMediaTypes is { Count: > 0 } ? rawUploadMediaTypes : [bodyContentType];
            sb.AppendLine("      body: file,");
            if (declared.Count == 1 && !declared[0].Contains('*', StringComparison.Ordinal))
            {
                sb.Append("      contentType: '").Append(declared[0]).AppendLine("',");
            }
            else
            {
                var fallback = declared.FirstOrDefault(m => !m.Contains('*', StringComparison.Ordinal)) ?? "application/octet-stream";
                sb.Append("      contentType: file.type && [")
                    .Append(string.Join(", ", declared.Select(m => "'" + m + "'")))
                    .Append("].some((t) => t === '*/*' || t === file.type || (t.endsWith('/*') && file.type.startsWith(t.slice(0, -1)))) ? file.type : '")
                    .Append(fallback)
                    .AppendLine("',");
            }

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

    /// <summary>
    /// A URL-encoded form body: the model's fields in a <c>URLSearchParams</c> (the runtime sends it as
    /// <c>application/x-www-form-urlencoded</c>), a list as one field per item, a <c>Date</c> (with
    /// <c>--convert-dates</c>) as its ISO text. A file has no URL-encoded form and is left out (ATC_API_OPR031
    /// reports it).
    /// </summary>
    private static void AppendUrlEncodedBody(
        StringBuilder sb,
        IOpenApiSchema bodySchema,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates)
    {
        sb.AppendLine("      body: (() => {");
        sb.AppendLine("        const form = new URLSearchParams();");

        var properties = bodySchema.Properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
        foreach (var prop in properties)
        {
            var (isFile, _) = prop.Value.GetFileUploadInfo();
            if (isFile)
            {
                continue;
            }

            var name = TypeScriptPropertyNames.Resolve(prop.Key, properties.Keys, namingStrategy);
            var access = TypeScriptOperationHelper.OptionalPropertyAccess("body", name).Replace("body?.", "body.", StringComparison.Ordinal);
            var itemSchema = prop.Value.IsOfType(JsonSchemaType.Array) ? prop.Value.Items : prop.Value;
            var isDate = convertDates && IsDate(itemSchema);
            if (prop.Value.IsOfType(JsonSchemaType.Array))
            {
                sb.Append("        for (const item of ").Append(access).Append(" ?? []) form.append('").Append(prop.Key).Append("', ").Append(Text("item", isDate, itemSchema)).AppendLine(");");
            }
            else
            {
                sb.Append("        if (").Append(access).Append(" != null) form.append('").Append(prop.Key).Append("', ").Append(Text(access, isDate, itemSchema)).AppendLine(");");
            }
        }

        sb.AppendLine("        return form;");
        sb.AppendLine("      })(),");

        static bool IsDate(IOpenApiSchema? schema)
            => schema?.IsOfType(JsonSchemaType.String) == true &&
               (string.Equals(schema.Format, "date-time", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(schema.Format, "date", StringComparison.OrdinalIgnoreCase));

        // The field's text: a Date as its ISO text (a date as its first ten characters), anything else as String()
        static string Text(
            string value,
            bool isDate,
            IOpenApiSchema? schema)
            => isDate
                ? $"{value} instanceof Date ? {value}.toISOString(){(string.Equals(schema?.Format, "date", StringComparison.OrdinalIgnoreCase) ? ".substring(0, 10)" : string.Empty)} : String({value})"
                : $"String({value})";
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

            // An array whose items are one value (form with explode: false, pipeDelimited, spaceDelimited): the items
            // joined on the declared delimiter (an array would be sent as repeated keys)
            if (param.GetArrayDelimiter() is { } delimiter)
            {
                coercion += "?.join('" + delimiter + "')";
            }

            // Use original name as key if different from the transformed name
            if (!(param.Name ?? string.Empty).Equals(propName, StringComparison.Ordinal))
            {
                sb.Append("        '").Append(param.Name).Append("': ").Append(TypeScriptOperationHelper.OptionalPropertyAccess("query", propName)).Append(coercion).AppendLine(",");
            }
            else
            {
                sb.Append("        ").Append(TypeScriptOperationHelper.PropertyKey(propName)).Append(": ").Append(TypeScriptOperationHelper.OptionalPropertyAccess("query", propName)).Append(coercion).AppendLine(",");
            }
        }

        sb.AppendLine("      },");
    }

    private static void AppendHeadersObject(
        StringBuilder sb,
        List<OpenApiParameter> headerParams,
        List<OpenApiParameter>? cookieParams,
        bool convertDates,
        TypeScriptNamingStrategy namingStrategy,
        string? accept = null)
    {
        // Header names are emitted verbatim (no naming-strategy transform). HTTP header
        // names commonly contain dashes — both the key on the wire and the inline-type
        // key must match the OpenAPI name exactly. Access uses bracket + optional-chaining
        // because the outer `headers?:` is optional.
        sb.AppendLine("      headers: {");
        if (accept is not null)
        {
            sb.Append("        Accept: '").Append(accept).AppendLine("',");
        }

        foreach (var param in headerParams)
        {
            var rawName = param.Name ?? string.Empty;
            var coercion = convertDates && TypeScriptOperationHelper.IsDateParam(param)
                ? "?" + TypeScriptOperationHelper.GetDateSerializationSuffix(param)
                : string.Empty;
            sb.Append("        '").Append(rawName).Append("': headers?.['").Append(rawName).Append("']").Append(coercion).AppendLine(",");
        }

        if (cookieParams is { Count: > 0 })
        {
            AppendCookieHeader(sb, cookieParams, namingStrategy);
        }

        sb.AppendLine("      },");
    }

    /// <summary>
    /// Emits a combined Cookie header entry from OAS 3.2 in:cookie parameters.
    /// </summary>
    /// <remarks>
    /// Browsers forbid setting the Cookie header via the fetch API (forbidden request
    /// header). The emitted code is intended for Node.js server-to-server use.
    /// </remarks>
    private static void AppendCookieHeader(
        StringBuilder sb,
        List<OpenApiParameter> cookieParams,
        TypeScriptNamingStrategy namingStrategy)
    {
        // The cookies object uses the same (naming-strategy) property names as its type (BuildCookieTypeInline).
        // A value is percent-encoded (the server decodes it), unless the parameter declares style: cookie (RFC 6265 as is).
        static string Value(
            OpenApiParameter param,
            string access)
            => param.Style == ParameterStyle.Cookie ? "${" + access + "}" : "${encodeURIComponent(String(" + access + "))}";

        sb.AppendLine("        // Note: browsers forbid setting the Cookie header via fetch; Node.js only.");
        if (cookieParams.Count == 1)
        {
            var param = cookieParams[0];
            var access = TypeScriptOperationHelper.OptionalPropertyAccess("cookies", (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
            var rawName = param.Name ?? string.Empty;
            sb.Append("        'Cookie': ").Append(access).Append(" != null ? `").Append(rawName).Append('=').Append(Value(param, access)).AppendLine("` : undefined,");
        }
        else
        {
            sb.AppendLine("        'Cookie': [");
            foreach (var param in cookieParams)
            {
                var access = TypeScriptOperationHelper.OptionalPropertyAccess("cookies", (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
                var rawName = param.Name ?? string.Empty;
                sb.Append("          ").Append(access).Append(" != null && `").Append(rawName).Append('=').Append(Value(param, access)).AppendLine("`,");
            }

            sb.AppendLine("        ].filter(Boolean).join('; ') || undefined,");
        }
    }
}