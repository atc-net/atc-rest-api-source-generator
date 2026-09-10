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
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase,
        bool brandedIds = false)
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

            var hookInfos = CollectHookInfos(operations, openApiDoc);
            if (hookInfos.Count == 0)
            {
                continue;
            }

            var content = GenerateHookFile(
                segment,
                hookInfos,
                headerContent,
                namingStrategy,
                brandedIds);

            var fileName = $"use{segment}";
            results.Add((fileName, content));
        }

        return results;
    }

    private static List<SwrHookInfo> CollectHookInfos(
        List<(string Path, string Method, OpenApiOperation Operation)> operations,
        OpenApiDocument openApiDoc)
    {
        var hookInfos = new List<SwrHookInfo>();

        foreach (var (path, method, operation) in operations)
        {
            var operationId = operation.OperationId;
            if (string.IsNullOrEmpty(operationId))
            {
                continue;
            }

            var isStreaming = operation.IsStreamingResponse();
            var isFileDownload = operation.HasFileDownload();
            var returnType = TypeScriptOperationHelper.GetReturnType(operation, isStreaming, isFileDownload);
            var httpMethod = method.ToUpperInvariant();

            // The OpenAPI 3.2 QUERY method is a read — safe and idempotent — that carries its
            // criteria in the request body because they are too large for a URL. It belongs
            // with GET in useSWR, not in useSWRMutation.
            var isQuery = httpMethod is "GET" or "QUERY" && !isFileDownload && !isStreaming;

            // Everything else that changes state is a mutation. Listing only the four classic
            // verbs meant OpenAPI 3.2 additionalOperations verbs (LINK, UNLINK, ...) matched
            // neither set and were dropped without a hook and without a diagnostic. The safe
            // metadata verbs are excluded because a hook for them has no meaning.
            var isMutation = !isQuery &&
                             !isStreaming &&
                             httpMethod is not ("HEAD" or "OPTIONS" or "TRACE");

            if (!isQuery && !isMutation && !isStreaming)
            {
                continue;
            }

            // Streaming hooks need the operation's path/query/header params at emission
            // time so the generated useState/useEffect-backed hook can forward them to the
            // async-generator client method. Non-streaming hooks ignore these, but it's
            // cheaper to populate them unconditionally than to branch the record shape.
            var pathParams = TypeScriptOperationHelper.GetMergedParameters(operation, openApiDoc, path, ParameterLocation.Path);
            var queryParams = TypeScriptOperationHelper.GetMergedParameters(operation, openApiDoc, path, ParameterLocation.Query);
            var headerParams = TypeScriptOperationHelper.GetMergedParameters(operation, openApiDoc, path, ParameterLocation.Header);
            var methodName = operationId.ToCamelCase().ToTypeScriptIdentifier();

            // A read with a body needs the body in the hook signature, the client call and the
            // cache key; a mutation forwards its argument untyped, so only the read path uses
            // the resolved type name.
            var (bodySchema, _) = operation.GetRequestBodySchemaWithContentType();
            var hasBody = bodySchema is not null;
            var bodyType = hasBody && isQuery
                ? bodySchema!.ToTypeScriptReturnType()
                : string.Empty;

            // The generated client method takes zero arguments when the operation has no path
            // params, no body and nothing to send in the query string or headers. A mutation
            // hook must then call it with no argument at all — forwarding `arg as never` to a
            // zero-parameter method is a TypeScript compile error in the consumer's project.
            var takesArgument = pathParams.Count > 0 ||
                                queryParams.Count > 0 ||
                                headerParams.Count > 0 ||
                                hasBody;

            hookInfos.Add(new SwrHookInfo(
                operationId,
                returnType,
                isQuery,
                isMutation,
                isStreaming,
                path,
                methodName,
                pathParams,
                queryParams,
                headerParams,
                operation.Summary,
                operation.Description,
                operation.Deprecated,
                hasBody,
                bodyType,
                takesArgument));
        }

        return hookInfos;
    }

    private static string GenerateHookFile(
        string segment,
        List<SwrHookInfo> hookInfos,
        string? headerContent,
        TypeScriptNamingStrategy namingStrategy,
        bool brandedIds)
    {
        var sb = new StringBuilder();
        var brandImports = new SortedSet<string>(StringComparer.Ordinal);

        // Pre-scan path params so the BrandedIds import line is accurate.
        if (brandedIds)
        {
            foreach (var info in hookInfos)
            {
                foreach (var param in info.PathParams)
                {
                    if (string.IsNullOrEmpty(param.Name))
                    {
                        continue;
                    }

                    var brand = TypeScriptBrandedIdExtractor.ResolveParamBrand(info.Path, param.Name, param.Schema);
                    if (brand is not null)
                    {
                        brandImports.Add(brand);
                    }
                }
            }
        }

        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        // Imports
        var hasQueries = hookInfos.Any(h => h.IsQuery);
        var hasMutations = hookInfos.Any(h => h.IsMutation);
        var hasStreaming = hookInfos.Any(h => h.IsStreaming);

        // Streaming hooks are framework-agnostic React + AbortController code — they
        // don't go through useSWR at all. React primitive hooks come first so the
        // import order matches what consumers expect.
        if (hasStreaming)
        {
            sb.AppendLine("import { useCallback, useEffect, useRef, useState } from 'react';");
        }

        if (hasQueries)
        {
            sb.AppendLine("import useSWR from 'swr';");
        }

        if (hasMutations)
        {
            sb.AppendLine("import useSWRMutation from 'swr/mutation';");
        }

        sb.AppendLine("import { useApiService } from './useApiService';");

        // Return types are never named in an SWR hook — the body reads result.status and
        // result.data with the type inferred — so nothing is imported for them. An earlier version
        // collected them and, on finding any, imported ApiResult, which the emitted code does not
        // reference either: an unused import that fails a consumer building with noUnusedLocals or
        // an ESLint no-unused-vars rule.
        //
        // Body types are different: they appear in a read hook's own signature, so they have
        // to be imported by name or the emitted TypeScript will not compile.
        var bodyImports = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var info in hookInfos)
        {
            if (!info.HasBody || string.IsNullOrEmpty(info.BodyType))
            {
                continue;
            }

            var cleanType = info.BodyType
                .Replace("[]", string.Empty, StringComparison.Ordinal)
                .Replace("?", string.Empty, StringComparison.Ordinal);
            if (char.IsUpper(cleanType[0]) && cleanType != "Blob")
            {
                bodyImports.Add(cleanType);
            }
        }

        if (bodyImports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", bodyImports)).AppendLine(" } from '../models';");
        }

        if (brandImports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", brandImports)).AppendLine(" } from '../types/BrandedIds';");
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
            var hookName = $"use{info.OperationId.EnsureFirstCharacterToUpper()}";
            var methodName = info.OperationId.EnsureFirstCharacterToLower();

            if (info.IsStreaming)
            {
                // React Query Option A treatment for SWR consumers — the
                // streaming hook is plain React + AbortController, no SWR machinery, so
                // the body is essentially the same shape as the React Query sibling.
                GenerateStreamHook(sb, info, segmentLower, namingStrategy, brandedIds);
            }
            else if (info.IsQuery)
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

        AppendHookJsDoc(sb, info);
        if (info.HasBody)
        {
            // OpenAPI 3.2 QUERY: the body is the query, so it identifies the cache entry.
            // SWR hashes the key structurally, so the body object goes in directly.
            sb.Append("export function ").Append(hookName).Append("(body: ").Append(info.BodyType).AppendLine(") {");
            sb.AppendLine("  const api = useApiService();");
            sb.AppendLine();
            sb.AppendLine("  return useSWR(");
            sb.Append("    [...").Append(segmentLower).Append("Keys.all, '").Append(methodName).AppendLine("', body] as const,");
            sb.AppendLine("    async () => {");
            sb.Append("      const result = await api.").Append(segmentLower).Append('.').Append(methodName).AppendLine("(body);");
        }
        else if (isDetail)
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
        AppendHookJsDoc(sb, info);
        sb.Append("export function ").Append(hookName).AppendLine("() {");
        sb.AppendLine("  const api = useApiService();");
        sb.AppendLine();
        sb.AppendLine("  return useSWRMutation(");
        sb.Append("    ").Append(segmentLower).AppendLine("Keys.all,");

        if (info.TakesArgument)
        {
            sb.AppendLine("    async (_key: string, { arg }: { arg: unknown }) => {");
            sb.Append("      return api.").Append(segmentLower).Append('.').Append(methodName).AppendLine("(arg as never);");
        }
        else
        {
            // The client method takes nothing, so the SWR mutation argument has nowhere to go.
            sb.AppendLine("    async () => {");
            sb.Append("      return api.").Append(segmentLower).Append('.').Append(methodName).AppendLine("();");
        }

        sb.AppendLine("    },");
        sb.AppendLine("  );");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits the Option A streaming hook (useState + useEffect + AbortController) for an
    /// async-enumerable operation. The body is framework-agnostic — same shape as the
    /// React Query sibling — so SWR consumers get the same `{ items, status, error,
    /// cancel, reset }` contract without an Option C-style opt-in flag.
    /// </summary>
    private static void GenerateStreamHook(
        StringBuilder sb,
        SwrHookInfo info,
        string segmentLower,
        TypeScriptNamingStrategy namingStrategy,
        bool brandedIds)
    {
        var hookName = "use" + info.MethodName.ToPascalCaseForDotNet() + "Stream";
        var itemType = string.IsNullOrEmpty(info.ReturnType) ? "unknown" : info.ReturnType;

        var hookParams = new List<string>();
        foreach (var p in info.PathParams)
        {
            var n = (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var t = TypeScriptOperationHelper.GetParameterType(p, convertDates: false, brandedIds: brandedIds, path: info.Path);
            hookParams.Add(n + ": " + t);
        }

        if (info.QueryParams.Count > 0)
        {
            var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy);
            hookParams.Add("query?: " + queryType);
        }

        if (info.HeaderParams.Count > 0)
        {
            var headerType = TypeScriptOperationHelper.BuildHeaderTypeInline(info.HeaderParams);
            hookParams.Add("headers?: " + headerType);
        }

        hookParams.Add("options?: { enabled?: boolean }");
        var hookParamStr = string.Join(", ", hookParams);

        // Client call args mirror the streaming client method: pathParams..., query?,
        // headers?, controller.signal.
        var callParts = new List<string>();
        foreach (var p in info.PathParams)
        {
            callParts.Add((p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
        }

        if (info.QueryParams.Count > 0)
        {
            callParts.Add("query");
        }

        if (info.HeaderParams.Count > 0)
        {
            callParts.Add("headers");
        }

        callParts.Add("controller.signal");
        var clientCallArgs = string.Join(", ", callParts);

        // useEffect deps: enabled flag + path params + (optional) JSON-serialized
        // query/header bags so reference-equal-but-value-different re-renders restart
        // the stream cleanly.
        var depParts = new List<string> { "enabled" };
        foreach (var p in info.PathParams)
        {
            depParts.Add((p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
        }

        var needsKeyDep = info.QueryParams.Count > 0 || info.HeaderParams.Count > 0;
        if (needsKeyDep)
        {
            depParts.Add("keyDep");
        }

        var depList = string.Join(", ", depParts);

        AppendHookJsDoc(sb, info);
        sb.Append("export function ").Append(hookName).Append('(').Append(hookParamStr).AppendLine(") {");
        sb.AppendLine("  const api = useApiService();");
        sb.Append("  const [items, setItems] = useState<readonly ").Append(itemType).AppendLine("[]>([]);");
        sb.AppendLine("  const [status, setStatus] = useState<'idle' | 'streaming' | 'success' | 'error'>('idle');");
        sb.AppendLine("  const [error, setError] = useState<Error | null>(null);");
        sb.AppendLine("  const controllerRef = useRef<AbortController | null>(null);");
        sb.AppendLine();
        sb.AppendLine("  const cancel = useCallback(() => {");
        sb.AppendLine("    controllerRef.current?.abort();");
        sb.AppendLine("    controllerRef.current = null;");
        sb.AppendLine("    setStatus('idle');");
        sb.AppendLine("  }, []);");
        sb.AppendLine();
        sb.AppendLine("  const reset = useCallback(() => {");
        sb.AppendLine("    cancel();");
        sb.AppendLine("    setItems([]);");
        sb.AppendLine("    setError(null);");
        sb.AppendLine("  }, [cancel]);");
        sb.AppendLine();
        sb.AppendLine("  const enabled = options?.enabled !== false;");

        if (needsKeyDep)
        {
            sb.Append("  const keyDep = JSON.stringify({");
            if (info.QueryParams.Count > 0)
            {
                sb.Append(" query");
            }

            if (info.HeaderParams.Count > 0)
            {
                sb.Append(info.QueryParams.Count > 0 ? ", headers" : " headers");
            }

            sb.AppendLine(" });");
        }

        sb.AppendLine();
        sb.AppendLine("  useEffect(() => {");
        sb.AppendLine("    if (!enabled) return;");
        sb.AppendLine();
        sb.AppendLine("    const controller = new AbortController();");
        sb.AppendLine("    controllerRef.current = controller;");
        sb.AppendLine("    setStatus('streaming');");
        sb.AppendLine("    setItems([]);");
        sb.AppendLine("    setError(null);");
        sb.AppendLine();
        sb.Append("    let buffer: ").Append(itemType).AppendLine("[] = [];");
        sb.AppendLine("    let flushTimer: ReturnType<typeof setTimeout> | null = null;");
        sb.AppendLine("    const flush = () => { flushTimer = null; if (!controller.signal.aborted) setItems(buffer.slice()); };");
        sb.AppendLine("    (async () => {");
        sb.AppendLine("      try {");
        sb.Append("        for await (const item of api.").Append(segmentLower).Append('.').Append(info.MethodName).Append('(').Append(clientCallArgs).AppendLine(")) {");
        sb.AppendLine("          if (controller.signal.aborted) return;");
        sb.AppendLine("          buffer.push(item);");
        sb.AppendLine("          if (flushTimer === null) flushTimer = setTimeout(flush, 200);");
        sb.AppendLine("        }");
        sb.AppendLine("        if (!controller.signal.aborted) {");
        sb.AppendLine("          if (flushTimer !== null) clearTimeout(flushTimer);");
        sb.AppendLine("          setItems(buffer.slice());");
        sb.AppendLine("          setStatus('success');");
        sb.AppendLine("        }");
        sb.AppendLine("      } catch (err) {");
        sb.AppendLine("        if (controller.signal.aborted) return;");
        sb.AppendLine("        if (err instanceof DOMException && err.name === 'AbortError') return;");
        sb.AppendLine("        if (flushTimer !== null) clearTimeout(flushTimer);");
        sb.AppendLine("        setItems(buffer.slice());");
        sb.AppendLine("        setError(err as Error);");
        sb.AppendLine("        setStatus('error');");
        sb.AppendLine("      }");
        sb.AppendLine("    })();");
        sb.AppendLine();
        sb.AppendLine("    return () => { controller.abort(); if (flushTimer !== null) clearTimeout(flushTimer); };");
        sb.AppendLine("    // eslint-disable-next-line react-hooks/exhaustive-deps");
        sb.Append("  }, [").Append(depList).AppendLine("]);");
        sb.AppendLine();
        sb.AppendLine("  return { items, status, error, cancel, reset };");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private sealed record SwrHookInfo(
        string OperationId,
        string ReturnType,
        bool IsQuery,
        bool IsMutation,
        bool IsStreaming,
        string Path,
        string MethodName,
        List<OpenApiParameter> PathParams,
        List<OpenApiParameter> QueryParams,
        List<OpenApiParameter> HeaderParams,
        string? Summary,
        string? Description,
        bool Deprecated,
        bool HasBody,
        string BodyType,
        bool TakesArgument);

    private static void AppendHookJsDoc(
        StringBuilder sb,
        SwrHookInfo info)
    {
        var description = !string.IsNullOrWhiteSpace(info.Summary)
            ? info.Summary
            : info.Description;
        if (string.IsNullOrWhiteSpace(description) && !info.Deprecated)
        {
            return;
        }

        var jsDoc = new JsDocComment(
            description,
            parameters: null,
            returns: null,
            isDeprecated: info.Deprecated,
            deprecatedMessage: null,
            example: null);
        var rendered = new JsDocCommentGenerator().GenerateTags(indentSpaces: 0, jsDoc);
        if (!string.IsNullOrEmpty(rendered))
        {
            sb.Append(rendered);
        }
    }
}