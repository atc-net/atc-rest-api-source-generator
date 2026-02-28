// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates per-segment TanStack Query (React Query) hook files from OpenAPI operations.
/// GET operations become useQuery hooks; POST/PUT/PATCH/DELETE become useMutation hooks.
/// Streaming operations (x-return-async-enumerable) are skipped with a comment.
/// </summary>
[SuppressMessage("Design", "MA0051:Method is too long", Justification = "Code generation methods require sequential StringBuilder operations.")]
public static class TypeScriptReactQueryHookExtractor
{
    /// <summary>
    /// Extracts all per-segment hook files from the OpenAPI document.
    /// </summary>
    /// <param name="openApiDoc">The parsed OpenAPI document.</param>
    /// <param name="headerContent">Optional auto-generated file header.</param>
    /// <param name="enumNames">Names of types that are enums (for correct import type).</param>
    /// <returns>List of (FileName, FileContent) tuples for each segment hook file.</returns>
    public static List<(string FileName, string Content)> Extract(
        OpenApiDocument openApiDoc,
        string? headerContent,
        HashSet<string>? enumNames = null,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        var results = new List<(string FileName, string Content)>();
        var segments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);

        foreach (var segment in segments)
        {
            var operations = PathSegmentHelper.GetOperationsForSegment(openApiDoc, segment);
            if (operations.Count == 0)
            {
                continue;
            }

            var fileName = "use" + segment;
            var content = GenerateSegmentHooks(
                segment,
                operations,
                openApiDoc,
                headerContent,
                enumNames,
                namingStrategy);
            results.Add((fileName, content));
        }

        return results;
    }

    private static string GenerateSegmentHooks(
        string segment,
        List<(string Path, string Method, OpenApiOperation Operation)> operations,
        OpenApiDocument openApiDoc,
        string? headerContent,
        HashSet<string>? enumNames,
        TypeScriptNamingStrategy namingStrategy)
    {
        var sb = new StringBuilder();
        var importTypes = new HashSet<string>(StringComparer.Ordinal);
        var needsUseQuery = false;
        var needsUseMutation = false;
        var needsUseQueryClient = false;

        // Classify operations first to determine imports
        var hookInfos = new List<HookInfo>();
        foreach (var (path, method, operation) in operations)
        {
            var info = ClassifyOperation(path, method, operation, openApiDoc, namingStrategy);
            hookInfos.Add(info);

            if (info.IsSkipped)
            {
                continue;
            }

            TypeScriptOperationHelper.CollectImportTypes(operation, importTypes);

            if (info.IsQuery)
            {
                needsUseQuery = true;
            }
            else
            {
                needsUseMutation = true;
                needsUseQueryClient = true;
            }
        }

        // Write header
        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        // Write imports
        AppendImports(
            sb,
            importTypes,
            enumNames,
            needsUseQuery,
            needsUseMutation,
            needsUseQueryClient);

        // Write query key factory
        var segmentCamel = segment.ToCamelCase();
        AppendQueryKeyFactory(sb, segmentCamel, hookInfos, namingStrategy);

        // Write hook functions
        foreach (var info in hookInfos)
        {
            sb.AppendLine();
            if (info.IsSkipped)
            {
                sb.Append("// Streaming operation ").Append(info.MethodName).AppendLine(" is skipped.");
            }
            else if (info.IsQuery)
            {
                AppendQueryHook(sb, info, segmentCamel, namingStrategy);
            }
            else
            {
                AppendMutationHook(sb, info, segmentCamel, namingStrategy);
            }
        }

        return sb.ToString();
    }

    private static void AppendImports(
        StringBuilder sb,
        HashSet<string> importTypes,
        HashSet<string>? enumNames,
        bool needsUseQuery,
        bool needsUseMutation,
        bool needsUseQueryClient)
    {
        // TanStack Query imports
        var queryImports = new List<string>();
        if (needsUseQuery)
        {
            queryImports.Add("useQuery");
        }

        if (needsUseMutation)
        {
            queryImports.Add("useMutation");
        }

        if (needsUseQueryClient)
        {
            queryImports.Add("useQueryClient");
        }

        if (queryImports.Count > 0)
        {
            sb.Append("import { ").Append(string.Join(", ", queryImports)).AppendLine(" } from '@tanstack/react-query';");
        }

        sb.AppendLine("import { useApiService } from './useApiService';");
        sb.AppendLine("import { ApiError } from '../errors/ApiError';");

        // Model and enum imports
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

    private static void AppendQueryKeyFactory(
        StringBuilder sb,
        string segmentCamel,
        List<HookInfo> hookInfos,
        TypeScriptNamingStrategy namingStrategy)
    {
        sb.Append("const ").Append(segmentCamel).AppendLine("Keys = {");
        sb.Append("  all: ['").Append(segmentCamel).AppendLine("'] as const,");

        foreach (var info in hookInfos)
        {
            if (info.IsSkipped || !info.IsQuery)
            {
                continue;
            }

            var keyName = DeriveKeyName(info.MethodName, segmentCamel);

            if (info.PathParams.Count > 0)
            {
                // Detail-style key with path params
                var paramList = string.Join(
                    ", ",
                    info.PathParams.Select(p => (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy) + ": " + TypeScriptOperationHelper.GetParameterType(p)));
                var keyArgs = string.Join(
                    ", ",
                    info.PathParams.Select(p => (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy)));

                sb.Append("  ").Append(keyName).Append(": (").Append(paramList).Append(") => [...").Append(segmentCamel).Append("Keys.all, '").Append(keyName).Append("', ").Append(keyArgs).AppendLine("] as const,");
            }
            else if (info.QueryParams.Count > 0)
            {
                // List-style key with query params
                var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy);
                sb.Append("  ").Append(keyName).Append(": (query?: ").Append(queryType).Append(") => [...").Append(segmentCamel).Append("Keys.all, '").Append(keyName).Append("', query").AppendLine("] as const,");
            }
            else
            {
                // Simple key with no params
                sb.Append("  ").Append(keyName).Append(": () => [...").Append(segmentCamel).Append("Keys.all, '").Append(keyName).AppendLine("'] as const,");
            }
        }

        sb.AppendLine("};");
        sb.AppendLine();
        sb.Append("export { ").Append(segmentCamel).AppendLine("Keys };");
    }

    private static void AppendQueryHook(
        StringBuilder sb,
        HookInfo info,
        string segmentCamel,
        TypeScriptNamingStrategy namingStrategy)
    {
        var hookName = "use" + info.MethodName.ToPascalCaseForDotNet();
        var keyName = DeriveKeyName(info.MethodName, segmentCamel);
        var segmentProperty = segmentCamel;

        // Build hook parameter list
        var hookParams = new List<string>();
        foreach (var param in info.PathParams)
        {
            var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var paramType = TypeScriptOperationHelper.GetParameterType(param);
            hookParams.Add(paramName + ": " + paramType);
        }

        if (info.QueryParams.Count > 0)
        {
            var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy);
            hookParams.Add("query?: " + queryType);
        }

        var hookParamStr = string.Join(", ", hookParams);

        // Build key args
        string keyCallArgs;
        if (info.PathParams.Count > 0)
        {
            keyCallArgs = string.Join(
                ", ",
                info.PathParams.Select(p => (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy)));
        }
        else if (info.QueryParams.Count > 0)
        {
            keyCallArgs = "query";
        }
        else
        {
            keyCallArgs = string.Empty;
        }

        // Build client call args
        var clientCallArgs = BuildClientCallArgs(info.PathParams, info.QueryParams, hasBody: false, namingStrategy: namingStrategy);

        sb.Append("export function ").Append(hookName).Append('(').Append(hookParamStr).AppendLine(") {");
        sb.AppendLine("  const api = useApiService();");
        sb.AppendLine("  return useQuery({");
        sb.Append("    queryKey: ").Append(segmentCamel).Append("Keys.").Append(keyName).Append('(').Append(keyCallArgs).AppendLine("),");
        sb.AppendLine("    queryFn: async () => {");
        sb.Append("      const result = await api.").Append(segmentProperty).Append('.').Append(info.MethodName).Append('(').Append(clientCallArgs).AppendLine(");");

        AppendResultUnwrap(sb, info.ReturnType, info.HttpMethod);

        sb.AppendLine("    },");

        // Add enabled guard for detail queries with path params
        if (info.PathParams.Count > 0)
        {
            var firstParam = (info.PathParams[0].Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            sb.Append("    enabled: !!").Append(firstParam).AppendLine(",");
        }

        sb.AppendLine("  });");
        sb.AppendLine("}");
    }

    private static void AppendMutationHook(
        StringBuilder sb,
        HookInfo info,
        string segmentCamel,
        TypeScriptNamingStrategy namingStrategy)
    {
        var hookName = "use" + info.MethodName.ToPascalCaseForDotNet();
        var segmentProperty = segmentCamel;
        var isVoidReturn = info.ReturnType == "void";
        var isDelete = info.HttpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase);

        // Determine hook signature params (path params that are "stable" go as hook params)
        // and mutation fn arg (body or path params for delete)
        var hookParams = new List<string>();
        string mutationArg;
        string clientCallArgs;

        if (info.HasBody && info.PathParams.Count > 0)
        {
            // Path params as hook params, body as mutation arg
            foreach (var param in info.PathParams)
            {
                var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
                var paramType = TypeScriptOperationHelper.GetParameterType(param);
                hookParams.Add(paramName + ": " + paramType);
            }

            mutationArg = "(body: " + info.BodyType + ")";
            clientCallArgs = BuildClientCallArgs(info.PathParams, info.QueryParams, hasBody: true, namingStrategy: namingStrategy);
        }
        else if (info.HasBody)
        {
            // Body only as mutation arg
            mutationArg = "(body: " + info.BodyType + ")";
            clientCallArgs = BuildClientCallArgs(info.PathParams, info.QueryParams, hasBody: true, namingStrategy: namingStrategy);
        }
        else if (info.HasFileUploadArg)
        {
            // File upload arg as mutation arg
            mutationArg = "(" + info.FileUploadParam + ")";
            clientCallArgs = info.FileUploadArgName;
        }
        else if (info.PathParams.Count > 0)
        {
            // Path params as mutation args (e.g., delete by ID)
            if (info.PathParams.Count == 1)
            {
                var param = info.PathParams[0];
                var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
                var paramType = TypeScriptOperationHelper.GetParameterType(param);
                mutationArg = "(" + paramName + ": " + paramType + ")";
                clientCallArgs = paramName;
            }
            else
            {
                var paramParts = info.PathParams.Select(p =>
                {
                    var pName = (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
                    var pType = TypeScriptOperationHelper.GetParameterType(p);
                    return pName + ": " + pType;
                });
                mutationArg = "(params: { " + string.Join("; ", paramParts) + " })";
                clientCallArgs = string.Join(
                    ", ",
                    info.PathParams.Select(p => "params." + (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy)));
            }
        }
        else
        {
            // No args
            mutationArg = "()";
            clientCallArgs = string.Empty;
        }

        var hookParamStr = string.Join(", ", hookParams);

        sb.Append("export function ").Append(hookName).Append('(').Append(hookParamStr).AppendLine(") {");
        sb.AppendLine("  const api = useApiService();");
        sb.AppendLine("  const queryClient = useQueryClient();");
        sb.AppendLine("  return useMutation({");
        sb.Append("    mutationFn: async ").Append(mutationArg).AppendLine(" => {");
        sb.Append("      const result = await api.").Append(segmentProperty).Append('.').Append(info.MethodName).Append('(').Append(clientCallArgs).AppendLine(");");

        AppendResultUnwrap(sb, info.ReturnType, info.HttpMethod);

        sb.AppendLine("    },");
        sb.AppendLine("    onSuccess: () => {");
        sb.Append("      queryClient.invalidateQueries({ queryKey: ").Append(segmentCamel).AppendLine("Keys.all });");
        sb.AppendLine("    },");
        sb.AppendLine("  });");
        sb.AppendLine("}");
    }

    private static void AppendResultUnwrap(
        StringBuilder sb,
        string returnType,
        string httpMethod)
    {
        var isVoid = returnType == "void";
        var isDelete = httpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase);

        if (isVoid || isDelete)
        {
            sb.AppendLine("      if (result.status === 'noContent' || result.status === 'ok') {");
            sb.AppendLine("        return;");
        }
        else
        {
            sb.AppendLine("      if (result.status === 'ok' || result.status === 'created') {");
            sb.AppendLine("        return result.data;");
        }

        sb.AppendLine("      }");
        sb.AppendLine("      throw new ApiError(");
        sb.AppendLine("        result.response.status,");
        sb.AppendLine("        result.response.statusText,");
        sb.AppendLine("        'error' in result ? result.error.message : 'Request failed',");
        sb.AppendLine("        result.response,");
        sb.AppendLine("      );");
    }

    private static string BuildClientCallArgs(
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        bool hasBody,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase)
    {
        var args = new List<string>();

        foreach (var param in pathParams)
        {
            args.Add((param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
        }

        if (hasBody)
        {
            args.Add("body");
        }

        if (queryParams.Count > 0)
        {
            args.Add("query");
        }

        return string.Join(", ", args);
    }

    /// <summary>
    /// Derives a query key entry name from an operationId.
    /// Strips the segment prefix and converts remainder to camelCase.
    /// Examples: listAccounts -> list, getAccountById -> detail, listPaginatedAccounts -> listPaginated.
    /// </summary>
    private static string DeriveKeyName(
        string methodName,
        string segmentCamel)
    {
        // The methodName is already camelCase (e.g., listAccounts, getAccountById)
        // Try to strip the segment suffix (case-insensitive)
        var segmentPascal = segmentCamel.ToPascalCaseForDotNet();

        // Check if this is a "get...By..." pattern -> detail
        if (methodName.StartsWith("get", StringComparison.OrdinalIgnoreCase) &&
            methodName.Contains("By", StringComparison.Ordinal))
        {
            return "detail";
        }

        // Strip segment name from the method name
        // e.g., "listAccounts" with segment "accounts" -> "list"
        // e.g., "listPaginatedAccounts" with segment "accounts" -> "listPaginated"
        var result = methodName;

        // Remove trailing segment name (PascalCase form)
        if (result.EndsWith(segmentPascal, StringComparison.Ordinal))
        {
            result = result[..^segmentPascal.Length];
        }

        // If nothing left, use the full method name
        if (string.IsNullOrEmpty(result))
        {
            result = methodName;
        }

        return result;
    }

    private static HookInfo ClassifyOperation(
        string path,
        string httpMethod,
        OpenApiOperation operation,
        OpenApiDocument openApiDoc,
        TypeScriptNamingStrategy namingStrategy)
    {
        var isStreaming = operation.IsAsyncEnumerableOperation();
        var isFileDownload = operation.HasFileDownload();
        var isFileUpload = operation.HasFileUpload();
        var operationId = operation.GetOperationId(path, httpMethod);
        var methodName = operationId.ToCamelCase();

        var pathParams = TypeScriptOperationHelper.GetMergedParameters(
            operation, openApiDoc, path, ParameterLocation.Path);
        var queryParams = TypeScriptOperationHelper.GetMergedParameters(
            operation, openApiDoc, path, ParameterLocation.Query);

        var returnType = TypeScriptOperationHelper.GetReturnType(operation, isStreaming, isFileDownload);

        var (bodySchema, bodyContentType) = operation.GetRequestBodySchemaWithContentType();
        var hasBody = bodySchema != null && !isFileUpload;
        var bodyType = hasBody ? bodySchema!.ToTypeScriptReturnType() : string.Empty;

        // Determine file upload parameter info
        var hasFileUploadArg = false;
        var fileUploadParam = string.Empty;
        var fileUploadArgName = string.Empty;
        if (isFileUpload && bodySchema != null)
        {
            (hasFileUploadArg, fileUploadParam, fileUploadArgName) = GetFileUploadInfo(bodySchema, bodyContentType, namingStrategy);
        }

        // GET + not file download + not streaming => useQuery
        // GET + file download => useMutation (user-triggered)
        // Streaming => skip
        // Everything else => useMutation
        var isQuery = httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase)
                      && !isFileDownload
                      && !isStreaming;

        return new HookInfo(
            MethodName: methodName,
            HttpMethod: httpMethod,
            IsQuery: isQuery,
            IsSkipped: isStreaming,
            PathParams: pathParams,
            QueryParams: queryParams,
            ReturnType: returnType,
            HasBody: hasBody,
            BodyType: bodyType,
            HasFileUploadArg: hasFileUploadArg,
            FileUploadParam: fileUploadParam,
            FileUploadArgName: fileUploadArgName);
    }

    private static (bool HasArg, string ParamDecl, string ArgName) GetFileUploadInfo(
        IOpenApiSchema bodySchema,
        string bodyContentType,
        TypeScriptNamingStrategy namingStrategy)
    {
        // Raw binary upload
        if (bodyContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return (true, "file: Blob | File", "file");
        }

        // Array of files
        if (bodySchema is OpenApiSchema { Type: var type } && type?.HasFlag(JsonSchemaType.Array) == true)
        {
            return (true, "files: (Blob | File)[]", "files");
        }

        // Object with file properties
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

            var typeStr = "{ " + string.Join("; ", formParts) + " }";
            return (true, "data: " + typeStr, "data");
        }

        return (true, "data: FormData", "data");
    }

    private sealed record HookInfo(
        string MethodName,
        string HttpMethod,
        bool IsQuery,
        bool IsSkipped,
        List<OpenApiParameter> PathParams,
        List<OpenApiParameter> QueryParams,
        string ReturnType,
        bool HasBody,
        string BodyType,
        bool HasFileUploadArg,
        string FileUploadParam,
        string FileUploadArgName);
}