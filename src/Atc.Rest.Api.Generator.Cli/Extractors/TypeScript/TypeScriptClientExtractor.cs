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
        HashSet<string>? writableSchemas = null)
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
            var content = GenerateClientClass(className, operations, openApiDoc, headerContent, enumNames, namingStrategy, convertDates, httpClient, writableSchemas);
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
        HashSet<string> writableSchemas)
    {
        var sb = new StringBuilder();
        var importTypes = new HashSet<string>(StringComparer.Ordinal);

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
            if (!operation.IsAsyncEnumerableOperation())
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
                            if (otherOp == operation || otherOp.IsAsyncEnumerableOperation())
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
        var perOpResultTypes = new Dictionary<OpenApiOperation, string>(ReferenceEqualityComparer.Instance);
        var perOpDeclarations = new List<string>();
        var perOpErrorImports = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (operationPath, methodVerb, operation) in operations)
        {
            if (operation.IsAsyncEnumerableOperation())
            {
                continue;
            }

            var isFileDownload = operation.HasFileDownload();
            var isTextDownload = !isFileDownload && operation.HasTextResponse();
            var operationId = operation.GetOperationId(operationPath, methodVerb);
            var methodName = operationId.ToCamelCase().ToTypeScriptIdentifier();
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

        // Write header
        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        // Write imports
        AppendImports(sb, importTypes, enumNames, perOpErrorImports, httpClient);

        // Emit per-operation result-type aliases before the class.
        foreach (var declaration in perOpDeclarations)
        {
            sb.Append(declaration);
            sb.AppendLine();
        }

        // Class declaration
        sb.Append("export class ").Append(className).AppendLine(" {");
        sb.AppendLine("  private readonly api: ApiClient;");
        sb.AppendLine();
        sb.AppendLine("  constructor(api: ApiClient) {");
        sb.AppendLine("    this.api = api;");
        sb.AppendLine("  }");

        // Generate methods
        foreach (var (path, method, operation) in operations)
        {
            sb.AppendLine();
            perOpResultTypes.TryGetValue(operation, out var resultTypeName);
            AppendMethod(sb, path, method, operation, openApiDoc, namingStrategy, convertDates, resultTypeName, writableSchemas);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendImports(
        StringBuilder sb,
        HashSet<string> importTypes,
        HashSet<string>? enumNames,
        HashSet<string> errorImports,
        TypeScriptHttpClient httpClient)
    {
        // The Axios variant references AxiosResponse in per-op result-type arms — the Fetch
        // variant uses the global Response type, so only the Axios path needs the import.
        if (httpClient == TypeScriptHttpClient.Axios)
        {
            sb.AppendLine("import type { AxiosResponse } from 'axios';");
        }

        // Import ApiClient
        sb.AppendLine("import { ApiClient } from './ApiClient';");

        // Build model imports and enum imports separately
        var modelImports = new SortedSet<string>(StringComparer.Ordinal);
        var enumImports = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var typeName in importTypes)
        {
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
        HashSet<string> writableSchemas)
    {
        var isStreaming = operation.IsAsyncEnumerableOperation();
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

        if (isStreaming)
        {
            AppendStreamingMethod(sb, methodName, path, pathParams, queryParams, headerParams, returnType, namingStrategy, convertDates);
        }
        else
        {
            AppendStandardMethod(sb, methodName, path, httpMethod, pathParams, queryParams, headerParams, bodySchema, bodyContentType, isFileUpload, isFileDownload, isTextDownload, returnType, namingStrategy, convertDates, perOpResultTypeName, writableSchemas);
        }
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
        HashSet<string> writableSchemas)
    {
        // Build parameter list
        var paramList = BuildParameterList(pathParams, queryParams, headerParams, bodySchema, bodyContentType, isFileUpload, namingStrategy, convertDates, writableSchemas);

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

        if (hasQuery || hasHeaders || hasBody || isFileUpload || isFileDownload || isTextDownload)
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
        bool convertDates)
    {
        // Build parameter list (streaming methods may have query / header params + signal)
        var paramParts = new List<string>();

        foreach (var param in pathParams)
        {
            var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var paramType = TypeScriptOperationHelper.GetParameterType(param, convertDates);
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
        HashSet<string> writableSchemas)
    {
        var parts = new List<string>();

        // Path parameters (required)
        foreach (var param in pathParams)
        {
            var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var paramType = TypeScriptOperationHelper.GetParameterType(param, convertDates);
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