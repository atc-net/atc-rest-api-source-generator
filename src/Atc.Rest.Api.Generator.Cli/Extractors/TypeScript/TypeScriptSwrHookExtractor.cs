// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates per-segment SWR hook files from OpenAPI operations.
/// GET operations become useSWR hooks; POST/PUT/PATCH/DELETE become useSWRMutation hooks.
/// Streaming operations are skipped with a comment.
/// </summary>
[SuppressMessage("Design", "MA0051:Method is too long", Justification = "Code generation methods require sequential StringBuilder operations.")]
public static class TypeScriptSwrHookExtractor
{
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

            var hookInfos = CollectHookInfos(operations, openApiDoc, segment, namingStrategy);
            if (hookInfos.Count == 0)
            {
                continue;
            }

            var content = GenerateHookFile(
                segment,
                hookInfos,
                headerContent,
                enumNames,
                namingStrategy);

            var fileName = $"use{segment}";
            results.Add((fileName, content));
        }

        return results;
    }

    private static List<SwrHookInfo> CollectHookInfos(
        List<(string Path, string Method, OpenApiOperation Operation)> operations,
        OpenApiDocument openApiDoc,
        string segment,
        TypeScriptNamingStrategy namingStrategy)
    {
        var hookInfos = new List<SwrHookInfo>();

        foreach (var (path, method, operation) in operations)
        {
            var operationId = operation.OperationId;
            if (string.IsNullOrEmpty(operationId))
            {
                continue;
            }

            var isStreaming = operation.IsAsyncEnumerableOperation();
            var isFileDownload = operation.HasFileDownload();
            var returnType = TypeScriptOperationHelper.GetReturnType(operation, isStreaming, isFileDownload);
            var httpMethod = method.ToUpperInvariant();

            var isQuery = httpMethod == "GET" && !isFileDownload && !isStreaming;
            var isMutation = httpMethod is "POST" or "PUT" or "PATCH" or "DELETE";

            if (!isQuery && !isMutation)
            {
                if (isStreaming)
                {
                    hookInfos.Add(new SwrHookInfo(operationId, returnType, IsQuery: false, IsMutation: false, IsSkipped: true));
                }

                continue;
            }

            hookInfos.Add(new SwrHookInfo(operationId, returnType, isQuery, isMutation, IsSkipped: false));
        }

        return hookInfos;
    }

    private static string GenerateHookFile(
        string segment,
        List<SwrHookInfo> hookInfos,
        string? headerContent,
        HashSet<string>? enumNames,
        TypeScriptNamingStrategy namingStrategy)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        // Imports
        var hasQueries = hookInfos.Any(h => h.IsQuery);
        var hasMutations = hookInfos.Any(h => h.IsMutation);

        if (hasQueries)
        {
            sb.AppendLine("import useSWR from 'swr';");
        }

        if (hasMutations)
        {
            sb.AppendLine("import useSWRMutation from 'swr/mutation';");
        }

        sb.AppendLine("import { useApiService } from './useApiService';");

        // Collect model imports
        var importTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var info in hookInfos)
        {
            if (!info.IsSkipped && info.ReturnType != "void" && info.ReturnType != "unknown")
            {
                var cleanType = info.ReturnType
                    .Replace("[]", string.Empty, StringComparison.Ordinal)
                    .Replace("?", string.Empty, StringComparison.Ordinal);
                if (char.IsUpper(cleanType[0]) && cleanType != "Blob")
                {
                    importTypes.Add(cleanType);
                }
            }
        }

        if (importTypes.Count > 0)
        {
            sb.AppendLine("import type { ApiResult } from '../types/ApiResult';");
        }

        sb.AppendLine();

        // Key factory
        var segmentLower = segment.EnsureFirstCharacterToLower();
        sb.Append("export const ").Append(segmentLower).AppendLine("Keys = {");
        sb.Append("  all: ['").Append(segmentLower).AppendLine("'] as const,");
        sb.Append("  detail: (id: string) => ['").Append(segmentLower).AppendLine("', id] as const,");
        sb.AppendLine("};");
        sb.AppendLine();

        // Generate hooks
        foreach (var info in hookInfos)
        {
            if (info.IsSkipped)
            {
                sb.Append("// Streaming operation ").Append(info.OperationId).AppendLine(" is skipped.");
                sb.AppendLine();
                continue;
            }

            var hookName = $"use{info.OperationId.EnsureFirstCharacterToUpper()}";
            var methodName = info.OperationId.EnsureFirstCharacterToLower();

            if (info.IsQuery)
            {
                GenerateQueryHook(sb, hookName, methodName, info, segmentLower);
            }
            else if (info.IsMutation)
            {
                GenerateMutationHook(sb, hookName, methodName, info, segmentLower);
            }
        }

        return sb.ToString();
    }

    private static void GenerateQueryHook(
        StringBuilder sb,
        string hookName,
        string methodName,
        SwrHookInfo info,
        string segmentLower)
    {
        var isDetail = info.OperationId.Contains("ById", StringComparison.OrdinalIgnoreCase) ||
                       info.OperationId.Contains("ByName", StringComparison.OrdinalIgnoreCase);

        if (isDetail)
        {
            sb.Append("export function ").Append(hookName).AppendLine("(id: string) {");
            sb.Append("  const api = useApiService();");
            sb.AppendLine();
            sb.AppendLine("  return useSWR(");
            sb.Append("    ").Append(segmentLower).AppendLine("Keys.detail(id),");
            sb.AppendLine("    async () => {");
            sb.Append("      const result = await api.").Append(segmentLower).Append('.').Append(methodName).AppendLine("(id);");
        }
        else
        {
            sb.Append("export function ").Append(hookName).AppendLine("() {");
            sb.AppendLine("  const api = useApiService();");
            sb.AppendLine();
            sb.AppendLine("  return useSWR(");
            sb.Append("    ").Append(segmentLower).AppendLine("Keys.all,");
            sb.AppendLine("    async () => {");
            sb.Append("      const result = await api.").Append(segmentLower).Append('.').Append(methodName).AppendLine("();");
        }

        sb.AppendLine("      if (result.status === 'ok' || result.status === 'created') {");
        sb.AppendLine("        return result.data;");
        sb.AppendLine("      }");
        sb.AppendLine("      throw result.error;");
        sb.AppendLine("    },");
        sb.AppendLine("  );");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void GenerateMutationHook(
        StringBuilder sb,
        string hookName,
        string methodName,
        SwrHookInfo info,
        string segmentLower)
    {
        sb.Append("export function ").Append(hookName).AppendLine("() {");
        sb.AppendLine("  const api = useApiService();");
        sb.AppendLine();
        sb.AppendLine("  return useSWRMutation(");
        sb.Append("    ").Append(segmentLower).AppendLine("Keys.all,");
        sb.AppendLine("    async (_key: string, { arg }: { arg: unknown }) => {");
        sb.Append("      return api.").Append(segmentLower).Append('.').Append(methodName).AppendLine("(arg as never);");
        sb.AppendLine("    },");
        sb.AppendLine("  );");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private sealed record SwrHookInfo(
        string OperationId,
        string ReturnType,
        bool IsQuery,
        bool IsMutation,
        bool IsSkipped);
}