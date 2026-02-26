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
        HashSet<string>? enumNames = null)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        var results = new List<(string ClassName, string Content)>();
        var segments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);

        foreach (var segment in segments)
        {
            var operations = PathSegmentHelper.GetOperationsForSegment(openApiDoc, segment);
            if (operations.Count == 0)
            {
                continue;
            }

            var className = segment + "Client";
            var content = GenerateClientClass(className, operations, openApiDoc, headerContent, enumNames);
            results.Add((className, content));
        }

        return results;
    }

    private static string GenerateClientClass(
        string className,
        List<(string Path, string Method, OpenApiOperation Operation)> operations,
        OpenApiDocument openApiDoc,
        string? headerContent,
        HashSet<string>? enumNames)
    {
        var sb = new StringBuilder();
        var importTypes = new HashSet<string>(StringComparer.Ordinal);

        // First pass: collect all import types
        foreach (var (_, _, operation) in operations)
        {
            CollectImportTypes(operation, importTypes);
        }

        // Write header
        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        // Write imports
        AppendImports(sb, importTypes, enumNames);

        // Class declaration
        sb.Append("export class ").Append(className).AppendLine(" {");
        sb.AppendLine("  constructor(private readonly api: ApiClient) {}");

        // Generate methods
        foreach (var (path, method, operation) in operations)
        {
            sb.AppendLine();
            AppendMethod(sb, path, method, operation, openApiDoc);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendImports(
        StringBuilder sb,
        HashSet<string> importTypes,
        HashSet<string>? enumNames)
    {
        // Import ApiClient
        sb.AppendLine("import { ApiClient } from './ApiClient';");

        // Import ApiResult (always needed for non-streaming)
        sb.AppendLine("import type { ApiResult } from '../types/ApiResult';");

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

        sb.AppendLine();
    }

    private static void AppendMethod(
        StringBuilder sb,
        string path,
        string httpMethod,
        OpenApiOperation operation,
        OpenApiDocument openApiDoc)
    {
        var isStreaming = operation.IsAsyncEnumerableOperation();
        var isFileDownload = operation.HasFileDownload();
        var isFileUpload = operation.HasFileUpload();
        var operationId = operation.GetOperationId(path, httpMethod);
        var methodName = operationId.ToCamelCase();

        // Get parameters (merge path-level and operation-level)
        var pathParams = GetMergedParameters(operation, openApiDoc, path, ParameterLocation.Path);
        var queryParams = GetMergedParameters(operation, openApiDoc, path, ParameterLocation.Query);

        // Get request body
        var (bodySchema, bodyContentType) = operation.GetRequestBodySchemaWithContentType();

        // Get response type
        var returnType = GetReturnType(operation, isStreaming, isFileDownload);

        if (isStreaming)
        {
            AppendStreamingMethod(sb, methodName, path, pathParams, queryParams, returnType);
        }
        else
        {
            AppendStandardMethod(sb, methodName, path, httpMethod, pathParams, queryParams, bodySchema, bodyContentType, isFileUpload, isFileDownload, returnType);
        }
    }

    private static void AppendStandardMethod(
        StringBuilder sb,
        string methodName,
        string path,
        string httpMethod,
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        IOpenApiSchema? bodySchema,
        string bodyContentType,
        bool isFileUpload,
        bool isFileDownload,
        string returnType)
    {
        // Build parameter list
        var paramList = BuildParameterList(pathParams, queryParams, bodySchema, bodyContentType, isFileUpload);
        sb.Append("  async ").Append(methodName).Append('(').Append(paramList).Append("): Promise<ApiResult<").Append(returnType).AppendLine(">> {");

        // Build path with interpolation
        var interpolatedPath = BuildInterpolatedPath(path, pathParams);

        // Build request options
        var hasQuery = queryParams.Count > 0;
        var hasBody = bodySchema != null;

        if (hasQuery || hasBody || isFileUpload || isFileDownload)
        {
            sb.Append("    return this.api.request<").Append(returnType).Append(">('").Append(httpMethod).Append("', ").Append(interpolatedPath).AppendLine(", {");

            if (hasBody && isFileUpload)
            {
                AppendFormDataBody(sb, bodySchema!, bodyContentType);
            }
            else if (hasBody)
            {
                sb.AppendLine("      body,");
            }

            if (hasQuery)
            {
                AppendQueryObject(sb, queryParams);
            }

            if (isFileDownload)
            {
                sb.AppendLine("      responseType: 'blob',");
            }

            sb.AppendLine("    });");
        }
        else
        {
            sb.Append("    return this.api.request<").Append(returnType).Append(">('").Append(httpMethod).Append("', ").Append(interpolatedPath).AppendLine(");");
        }

        sb.AppendLine("  }");
    }

    private static void AppendStreamingMethod(
        StringBuilder sb,
        string methodName,
        string path,
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        string itemType)
    {
        // Build parameter list (streaming methods may have query params + signal)
        var paramParts = new List<string>();

        foreach (var param in pathParams)
        {
            var paramName = (param.Name ?? string.Empty).ToCamelCase();
            var paramType = GetParameterType(param);
            paramParts.Add(paramName + ": " + paramType);
        }

        if (queryParams.Count > 0)
        {
            var queryType = BuildQueryTypeInline(queryParams);
            paramParts.Add("query?: " + queryType);
        }

        paramParts.Add("signal?: AbortSignal");

        var paramList = string.Join(", ", paramParts);

        sb.Append("  async *").Append(methodName).Append('(').Append(paramList).Append("): AsyncGenerator<").Append(itemType).AppendLine("> {");

        var interpolatedPath = BuildInterpolatedPath(path, pathParams);
        var hasQuery = queryParams.Count > 0;

        if (hasQuery)
        {
            sb.Append("    yield* this.api.requestStream<").Append(itemType).Append(">('GET', ").Append(interpolatedPath).AppendLine(", {");
            AppendQueryObject(sb, queryParams);
            sb.AppendLine("      signal,");
            sb.AppendLine("    });");
        }
        else
        {
            sb.Append("    yield* this.api.requestStream<").Append(itemType).Append(">('GET', ").Append(interpolatedPath).AppendLine(", { signal });");
        }

        sb.AppendLine("  }");
    }

    private static string GetReturnType(
        OpenApiOperation operation,
        bool isStreaming,
        bool isFileDownload)
    {
        if (isFileDownload)
        {
            return "Blob";
        }

        // Try to get 200 response schema, then 201
        var schema = operation.GetResponseSchema("200") ?? operation.GetResponseSchema("201");
        if (schema == null)
        {
            return isStreaming ? "unknown" : "void";
        }

        return schema.ToTypeScriptReturnType();
    }

    private static string BuildParameterList(
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        IOpenApiSchema? bodySchema,
        string bodyContentType,
        bool isFileUpload)
    {
        var parts = new List<string>();

        // Path parameters (required)
        foreach (var param in pathParams)
        {
            var paramName = (param.Name ?? string.Empty).ToCamelCase();
            var paramType = GetParameterType(param);
            parts.Add(paramName + ": " + paramType);
        }

        // Request body
        if (bodySchema != null)
        {
            if (isFileUpload)
            {
                AppendFileUploadParams(parts, bodySchema, bodyContentType);
            }
            else
            {
                var bodyType = bodySchema.ToTypeScriptReturnType();
                parts.Add("body: " + bodyType);
            }
        }

        // Query parameters (optional object)
        if (queryParams.Count > 0)
        {
            var queryType = BuildQueryTypeInline(queryParams);
            parts.Add("query?: " + queryType);
        }

        return string.Join(", ", parts);
    }

    private static void AppendFileUploadParams(
        List<string> parts,
        IOpenApiSchema bodySchema,
        string bodyContentType)
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
                var propName = prop.Key.ToCamelCase();
                var isRequired = required.Contains(prop.Key);
                var propType = prop.Value.ToTypeScriptTypeForModel(isRequired);

                // File properties: binary → Blob | File
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
        string bodyContentType)
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
                var propName = prop.Key.ToCamelCase();
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

    private static string BuildInterpolatedPath(
        string path,
        List<OpenApiParameter> pathParams)
    {
        if (pathParams.Count == 0)
        {
            return "'" + path + "'";
        }

        // Replace {paramName} with ${paramName} for template literal
        var interpolated = path;
        foreach (var param in pathParams)
        {
            var camelName = (param.Name ?? string.Empty).ToCamelCase();
            interpolated = interpolated.Replace(
                "{" + param.Name + "}",
                "${" + camelName + "}",
                StringComparison.Ordinal);
        }

        return "`" + interpolated + "`";
    }

    private static void AppendQueryObject(
        StringBuilder sb,
        List<OpenApiParameter> queryParams)
    {
        sb.AppendLine("      query: {");
        foreach (var param in queryParams)
        {
            var propName = (param.Name ?? string.Empty).ToCamelCase();

            // Use original name as key if different from camelCase
            if (!(param.Name ?? string.Empty).Equals(propName, StringComparison.Ordinal))
            {
                sb.Append("        '").Append(param.Name).Append("': query?.").Append(propName).AppendLine(",");
            }
            else
            {
                sb.Append("        ").Append(propName).Append(": query?.").Append(propName).AppendLine(",");
            }
        }

        sb.AppendLine("      },");
    }

    private static string BuildQueryTypeInline(
        List<OpenApiParameter> queryParams)
    {
        var parts = new List<string>();
        foreach (var param in queryParams)
        {
            var paramName = (param.Name ?? string.Empty).ToCamelCase();
            var paramType = GetParameterType(param);
            parts.Add(paramName + "?: " + paramType);
        }

        return "{ " + string.Join("; ", parts) + " }";
    }

    private static string GetParameterType(OpenApiParameter param)
    {
        if (param.Schema == null)
        {
            return "string";
        }

        var tsType = param.Schema.ToTypeScriptTypeForModel(isRequired: true);

        // Strip "| null" from query/path parameter types — URL parameters are either
        // present (with a value) or absent (undefined), never null.
        if (tsType.EndsWith(" | null", StringComparison.Ordinal))
        {
            tsType = tsType[..^" | null".Length];
        }

        return tsType;
    }

    /// <summary>
    /// Merges path-level and operation-level parameters by location.
    /// Resolves parameter references ($ref) to actual parameters.
    /// Operation-level parameters take precedence over path-level parameters with the same name.
    /// </summary>
    private static List<OpenApiParameter> GetMergedParameters(
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        string path,
        ParameterLocation location)
    {
        var result = new List<OpenApiParameter>();

        // Resolve operation-level parameters (handles both direct and $ref)
        var operationParams = ResolveParametersByLocation(operation.Parameters, location);
        var operationParamNames = new HashSet<string>(
            operationParams.Select(p => p.Name ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        // Add path-level parameters first (only those not overridden at operation level)
        if (openApiDoc.Paths != null &&
            openApiDoc.Paths.TryGetValue(path, out var pathItemValue) &&
            pathItemValue is OpenApiPathItem pathItem &&
            pathItem.Parameters != null)
        {
            var pathLevelParams = ResolveParametersByLocation(pathItem.Parameters, location);
            foreach (var param in pathLevelParams)
            {
                if (!operationParamNames.Contains(param.Name ?? string.Empty))
                {
                    result.Add(param);
                }
            }
        }

        // Add operation-level parameters
        result.AddRange(operationParams);

        return result;
    }

    /// <summary>
    /// Resolves a list of IOpenApiParameter (which may include $ref references) to concrete
    /// OpenApiParameter objects filtered by location.
    /// </summary>
    private static List<OpenApiParameter> ResolveParametersByLocation(
        IList<IOpenApiParameter>? parameters,
        ParameterLocation location)
    {
        var result = new List<OpenApiParameter>();
        if (parameters == null)
        {
            return result;
        }

        foreach (var paramInterface in parameters)
        {
            var resolved = paramInterface.Resolve();
            if (resolved.Parameter != null && resolved.Parameter.In == location)
            {
                result.Add(resolved.Parameter);
            }
        }

        return result;
    }

    private static void CollectImportTypes(
        OpenApiOperation operation,
        HashSet<string> importTypes)
    {
        // From response schemas (200, 201)
        CollectSchemaRefTypes(operation.GetResponseSchema("200"), importTypes);
        CollectSchemaRefTypes(operation.GetResponseSchema("201"), importTypes);

        // From request body
        var (bodySchema, _) = operation.GetRequestBodySchemaWithContentType();
        if (bodySchema != null)
        {
            CollectSchemaRefTypes(bodySchema, importTypes);

            // For multipart form data objects, also collect property types
            if (bodySchema.Properties is { Count: > 0 })
            {
                foreach (var prop in bodySchema.Properties)
                {
                    CollectSchemaRefTypes(prop.Value, importTypes);
                }
            }
        }
    }

    private static void CollectSchemaRefTypes(
        IOpenApiSchema? schema,
        HashSet<string> importTypes)
    {
        if (schema == null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference schemaRef)
        {
            var refName = schemaRef.Reference.Id ?? schemaRef.Id;
            if (refName != null)
            {
                importTypes.Add(refName);
            }

            return;
        }

        if (schema is not OpenApiSchema actualSchema)
        {
            return;
        }

        // Handle allOf references
        if (actualSchema.AllOf is { Count: > 0 })
        {
            foreach (var subSchema in actualSchema.AllOf)
            {
                if (subSchema is OpenApiSchemaReference allOfRef)
                {
                    var refName = allOfRef.Reference.Id ?? allOfRef.Id;
                    if (refName != null)
                    {
                        importTypes.Add(refName);
                    }
                }
            }
        }

        // Handle array item references
        if (actualSchema.Type?.HasFlag(JsonSchemaType.Array) == true && actualSchema.Items is OpenApiSchemaReference itemRef)
        {
            var refName = itemRef.Reference.Id ?? itemRef.Id;
            if (refName != null)
            {
                importTypes.Add(refName);
            }
        }
    }
}