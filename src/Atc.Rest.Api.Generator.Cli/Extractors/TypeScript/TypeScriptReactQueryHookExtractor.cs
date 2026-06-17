// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates per-segment TanStack Query (React Query) hook files from OpenAPI operations.
/// GET operations become useQuery hooks; POST/PUT/PATCH/DELETE become useMutation hooks.
/// Streaming operations (x-return-async-enumerable) become useXxxStream hooks backed by
/// useState + useEffect + AbortController. Hook contract: see issues/001-feedback.md.
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
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase,
        bool convertDates = false,
        HashSet<string>? writableSchemas = null,
        TypeScriptHooksMode hooksMode = TypeScriptHooksMode.Standard,
        bool brandedIds = false)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        var results = new List<(string FileName, string Content)>();
        var segments = PathSegmentHelper.GetUniquePathSegments(openApiDoc);
        writableSchemas ??= new HashSet<string>(StringComparer.Ordinal);

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
                namingStrategy,
                convertDates,
                writableSchemas,
                hooksMode,
                brandedIds);
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
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        HashSet<string> writableSchemas,
        TypeScriptHooksMode hooksMode,
        bool brandedIds)
    {
        var sb = new StringBuilder();
        var importTypes = new HashSet<string>(StringComparer.Ordinal);
        var brandImports = new SortedSet<string>(StringComparer.Ordinal);
        var needsUseQuery = false;
        var needsUseMutation = false;
        var needsUseQueryClient = false;
        var needsReactHooks = false;
        var needsApiError = false;

        // Classify operations first to determine imports
        var hookInfos = new List<HookInfo>();
        foreach (var (path, method, operation) in operations)
        {
            var info = ClassifyOperation(path, method, operation, openApiDoc, namingStrategy, writableSchemas);
            hookInfos.Add(info);

            // Pre-scan path params for brand qualification so the BrandedIds import line
            // is accurate before any hook body is emitted.
            if (brandedIds)
            {
                foreach (var param in info.PathParams)
                {
                    if (string.IsNullOrEmpty(param.Name))
                    {
                        continue;
                    }

                    var brand = TypeScriptBrandedIdExtractor.ResolveParamBrand(info.Path, param.Name!, param.Schema);
                    if (brand != null)
                    {
                        brandImports.Add(brand);
                    }
                }
            }

            // Streaming ops still need their item type imported.
            TypeScriptOperationHelper.CollectImportTypes(operation, importTypes, openApiDoc, path);

            // Match the client extractor: a body that's a $ref to a schema with the
            // readOnly/writeOnly split needs the <Name>Writable variant imported.
            var (bodySchema, _) = operation.GetRequestBodySchemaWithContentType();
            if (bodySchema is OpenApiSchemaReference bodyRef)
            {
                var refName = bodyRef.Reference.Id ?? bodyRef.Id;
                if (refName != null && writableSchemas.Contains(refName))
                {
                    importTypes.Add(refName + TypeScriptModelExtractor.WritableSuffix);
                }
            }

            if (info.IsStreaming)
            {
                needsReactHooks = true;
            }
            else if (info.IsQuery)
            {
                needsApiError = true;
            }
            else
            {
                needsUseMutation = true;
                needsUseQueryClient = true;
                needsApiError = true;
            }
        }

        // Mode gating: Standard / Both emits useQuery hooks; Suspense / Both emits
        // useSuspenseQuery hooks. needsUseQuery / needsUseSuspenseQuery only flip when
        // the file actually contains at least one query op, so imports stay minimal.
        var emitStandardQuery = hooksMode != TypeScriptHooksMode.Suspense;
        var emitSuspenseQuery = hooksMode != TypeScriptHooksMode.Standard;
        var hasAnyQuery = hookInfos.Any(h => h.IsQuery);
        needsUseQuery = emitStandardQuery && hasAnyQuery;
        var needsUseSuspenseQuery = emitSuspenseQuery && hasAnyQuery;

        // Paginated-streaming ops get a useInfiniteQuery sibling hook in addition to
        // the streaming hook. Detection is per-op; if any present, import useInfiniteQuery.
        var hasInfinite = hookInfos.Any(h => h.IsPaginatedStreaming);
        var needsUseInfiniteQuery = hasInfinite;
        if (hasInfinite)
        {
            // The infinite hook unwraps result.status === 'ok' the same way other hooks do,
            // so it needs ApiError available even if the file otherwise had only streams.
            needsApiError = true;
        }

        // Build the body (query-key factory + hooks) BEFORE the imports so model/enum
        // import lines can be narrowed to identifiers the body actually references. The
        // schema-graph scan can over-collect — e.g. an enum reachable only through an
        // inline request body, or an array-alias wrapper for a streamed item type — which
        // would otherwise fail tsc under noUnusedLocals.
        var bodySb = new StringBuilder();

        // Write query key factory
        var segmentCamel = segment.ToCamelCase();
        AppendQueryKeyFactory(bodySb, segmentCamel, hookInfos, namingStrategy, convertDates, brandedIds);

        // Write hook functions
        foreach (var info in hookInfos)
        {
            if (info.IsStreaming)
            {
                bodySb.AppendLine();
                AppendStreamHook(bodySb, info, segmentCamel, namingStrategy, convertDates, brandedIds);

                // Paginated-streaming ops also get a useInfiniteQuery sibling that
                // consumes the non-streaming Page companion emitted by the client extractor.
                if (info.IsPaginatedStreaming)
                {
                    bodySb.AppendLine();
                    AppendInfiniteHook(bodySb, info, segmentCamel, namingStrategy, convertDates, brandedIds);
                }
            }
            else if (info.IsQuery)
            {
                // For Both mode, emit standard and suspense as TWO separate hooks. For
                // single-mode emission the canonical hook name is used; the suffix only
                // disambiguates when both variants live in the same file.
                if (emitStandardQuery)
                {
                    bodySb.AppendLine();
                    AppendQueryHook(bodySb, info, segmentCamel, namingStrategy, convertDates, isSuspense: false, useSuspenseSuffix: false, brandedIds: brandedIds);
                }

                if (emitSuspenseQuery)
                {
                    bodySb.AppendLine();
                    AppendQueryHook(bodySb, info, segmentCamel, namingStrategy, convertDates, isSuspense: true, useSuspenseSuffix: hooksMode == TypeScriptHooksMode.Both, brandedIds: brandedIds);
                }
            }
            else
            {
                bodySb.AppendLine();
                AppendMutationHook(bodySb, info, segmentCamel, namingStrategy, convertDates, brandedIds);
            }
        }

        var bodyText = bodySb.ToString();

        // Write header
        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        // Write imports (narrowed to what the body references)
        AppendImports(
            sb,
            importTypes,
            enumNames,
            needsUseQuery,
            needsUseSuspenseQuery,
            needsUseMutation,
            needsUseQueryClient,
            needsReactHooks,
            needsApiError,
            needsUseInfiniteQuery,
            brandImports,
            bodyText);

        sb.Append(bodyText);

        return sb.ToString();
    }

    private static void AppendImports(
        StringBuilder sb,
        HashSet<string> importTypes,
        HashSet<string>? enumNames,
        bool needsUseQuery,
        bool needsUseSuspenseQuery,
        bool needsUseMutation,
        bool needsUseQueryClient,
        bool needsReactHooks,
        bool needsApiError,
        bool needsUseInfiniteQuery,
        SortedSet<string> brandImports,
        string bodyText)
    {
        // React primitive hooks (only required by stream hooks)
        if (needsReactHooks)
        {
            sb.AppendLine("import { useCallback, useEffect, useRef, useState } from 'react';");
        }

        // TanStack Query imports
        var queryImports = new List<string>();
        if (needsUseQuery)
        {
            queryImports.Add("useQuery");
        }

        if (needsUseSuspenseQuery)
        {
            queryImports.Add("useSuspenseQuery");
        }

        if (needsUseInfiniteQuery)
        {
            queryImports.Add("useInfiniteQuery");
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

        // Type-only options imports: every generated useQuery / useMutation hook accepts an
        // `options?` arg whose type omits the keys the generator already populates
        // (queryKey, queryFn for queries; mutationFn for mutations). Without these, callers
        // would need to wrap the hook to override staleTime, gcTime, onSuccess, etc.
        var optionImports = new List<string>();
        if (needsUseQuery)
        {
            optionImports.Add("UseQueryOptions");
        }

        if (needsUseSuspenseQuery)
        {
            optionImports.Add("UseSuspenseQueryOptions");
        }

        if (needsUseMutation)
        {
            optionImports.Add("UseMutationOptions");
        }

        if (optionImports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", optionImports)).AppendLine(" } from '@tanstack/react-query';");
        }

        sb.AppendLine("import { useApiService } from './useApiService';");

        // ApiError is only used by the result-unwrap path of useQuery/useMutation hooks.
        // Stream hooks surface errors as plain Error instances, so omit the import when
        // no non-stream hooks exist on this segment (otherwise tsconfig noUnusedLocals trips).
        if (needsApiError)
        {
            sb.AppendLine("import { ApiError } from '../errors/ApiError';");
        }

        // Model and enum imports — only those the hook body actually references, so an
        // over-collected enum/model (e.g. an array-alias wrapper for a streamed item type)
        // doesn't trip tsc under noUnusedLocals.
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

        if (brandImports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", brandImports)).AppendLine(" } from '../types/BrandedIds';");
        }

        sb.AppendLine();
    }

    private static void AppendQueryKeyFactory(
        StringBuilder sb,
        string segmentCamel,
        List<HookInfo> hookInfos,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        sb.Append("const ").Append(segmentCamel).AppendLine("Keys = {");
        sb.Append("  all: ['").Append(segmentCamel).AppendLine("'] as const,");

        foreach (var info in hookInfos)
        {
            // Paginated-streaming ops get an `<methodName>Infinite` key entry for
            // the useInfiniteQuery hook. The streaming hook itself still skips a key.
            if (info.IsPaginatedStreaming)
            {
                var infiniteKeyName = info.MethodName.ToCamelCase().ToTypeScriptIdentifier() + "Infinite";
                if (info.QueryParams.Count > 0)
                {
                    var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy, convertDates);
                    sb.Append("  ").Append(infiniteKeyName).Append(": (query?: ").Append(queryType).Append(") => [...").Append(segmentCamel).Append("Keys.all, '").Append(infiniteKeyName).Append("', query").AppendLine("] as const,");
                }
                else
                {
                    sb.Append("  ").Append(infiniteKeyName).Append(": () => [...").Append(segmentCamel).Append("Keys.all, '").Append(infiniteKeyName).AppendLine("'] as const,");
                }
            }

            // Stream hooks manage their own state and don't go through useQuery — no key needed.
            if (info.IsStreaming || !info.IsQuery)
            {
                continue;
            }

            var keyName = DeriveKeyName(info.MethodName, segmentCamel);
            var hasPath = info.PathParams.Count > 0;
            var hasQuery = info.QueryParams.Count > 0;

            if (hasPath && hasQuery)
            {
                // Detail/list hybrid — path params + query bag. Both must be in the key so
                // React Query refetches when either changes.
                var pathParamList = string.Join(
                    ", ",
                    info.PathParams.Select(p => (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy) + ": " + TypeScriptOperationHelper.GetParameterType(p, convertDates, brandedIds, info.Path)));
                var pathArgs = string.Join(
                    ", ",
                    info.PathParams.Select(p => (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy)));
                var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy, convertDates);

                sb.Append("  ").Append(keyName).Append(": (").Append(pathParamList).Append(", query?: ").Append(queryType).Append(") => [...").Append(segmentCamel).Append("Keys.all, '").Append(keyName).Append("', ").Append(pathArgs).AppendLine(", query] as const,");
            }
            else if (hasPath)
            {
                // Detail-style key with path params
                var paramList = string.Join(
                    ", ",
                    info.PathParams.Select(p => (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy) + ": " + TypeScriptOperationHelper.GetParameterType(p, convertDates, brandedIds, info.Path)));
                var keyArgs = string.Join(
                    ", ",
                    info.PathParams.Select(p => (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy)));

                sb.Append("  ").Append(keyName).Append(": (").Append(paramList).Append(") => [...").Append(segmentCamel).Append("Keys.all, '").Append(keyName).Append("', ").Append(keyArgs).AppendLine("] as const,");
            }
            else if (hasQuery)
            {
                // List-style key with query params
                var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy, convertDates);
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
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool isSuspense,
        bool useSuspenseSuffix,
        bool brandedIds)
    {
        // For TypeScriptHooksMode.Both the suspense variant gets a "Suspense" suffix so
        // standard and suspense hooks can coexist in the same file. For pure Suspense
        // mode the canonical hook name keeps its shape — the consumer just gets the
        // suspense semantics under the existing name.
        var baseHookName = "use" + info.MethodName.ToPascalCaseForDotNet();
        var hookName = useSuspenseSuffix ? baseHookName + "Suspense" : baseHookName;
        var queryFn = isSuspense ? "useSuspenseQuery" : "useQuery";
        var optionsType = isSuspense ? "UseSuspenseQueryOptions" : "UseQueryOptions";
        var keyName = DeriveKeyName(info.MethodName, segmentCamel);
        var segmentProperty = segmentCamel;

        // Build hook parameter list
        var hookParams = new List<string>();
        foreach (var param in info.PathParams)
        {
            var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var paramType = TypeScriptOperationHelper.GetParameterType(param, convertDates, brandedIds, info.Path);
            hookParams.Add(paramName + ": " + paramType);
        }

        if (info.QueryParams.Count > 0)
        {
            var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy, convertDates);
            hookParams.Add("query?: " + queryType);
        }

        if (info.HeaderParams.Count > 0)
        {
            var headerType = TypeScriptOperationHelper.BuildHeaderTypeInline(info.HeaderParams, convertDates);
            hookParams.Add("headers?: " + headerType);
        }

        // Pass-through options: every generated hook accepts a final options? arg whose
        // type omits the keys the hook body already populates. Consumers can override
        // staleTime, gcTime, select, refetchOnWindowFocus, etc. without wrapping. The
        // options type follows the underlying query function — UseQueryOptions for
        // useQuery, UseSuspenseQueryOptions for useSuspenseQuery (suspense flavor has a
        // different shape: no 'enabled', stricter on initialData, etc.).
        var queryDataType = info.ReturnType == "void" ? "void" : info.ReturnType;
        hookParams.Add("options?: Omit<" + optionsType + "<" + queryDataType + ", ApiError>, 'queryKey' | 'queryFn'>");

        var hookParamStr = string.Join(", ", hookParams);

        // Build key args — headers are intentionally excluded so the React Query cache
        // does NOT fragment on per-request headers (correlation IDs, etc). Query params
        // ARE included alongside path params so the cache correctly partitions per filter.
        var keyArgsParts = new List<string>();
        foreach (var p in info.PathParams)
        {
            keyArgsParts.Add((p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
        }

        if (info.QueryParams.Count > 0)
        {
            keyArgsParts.Add("query");
        }

        var keyCallArgs = string.Join(", ", keyArgsParts);

        // Build client call args (headers ARE forwarded to the client method)
        var clientCallArgs = BuildClientCallArgs(info.PathParams, info.QueryParams, info.HeaderParams, hasBody: false, namingStrategy: namingStrategy);

        AppendHookJsDoc(sb, info);
        sb.Append("export function ").Append(hookName).Append('(').Append(hookParamStr).AppendLine(") {");
        sb.AppendLine("  const api = useApiService();");
        sb.Append("  return ").Append(queryFn).AppendLine("({");
        sb.Append("    queryKey: ").Append(segmentCamel).Append("Keys.").Append(keyName).Append('(').Append(keyCallArgs).AppendLine("),");
        sb.AppendLine("    queryFn: async () => {");
        sb.Append("      const result = await api.").Append(segmentProperty).Append('.').Append(info.MethodName).Append('(').Append(clientCallArgs).AppendLine(");");

        AppendResultUnwrap(sb, info.ReturnType, info.HttpMethod, info.Success2xxDiscriminators);

        sb.AppendLine("    },");

        // Add enabled guard for detail queries with path params — but only for the
        // standard variant. useSuspenseQuery doesn't support conditional execution
        // (it throws a promise that the boundary catches), so consumers control whether
        // the hook runs by mounting/unmounting the component, not by toggling enabled.
        if (!isSuspense && info.PathParams.Count > 0)
        {
            var firstParam = (info.PathParams[0].Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            sb.Append("    enabled: !!").Append(firstParam).AppendLine(",");
        }

        // Spread caller options LAST so they override the generated defaults (enabled,
        // queryKey, queryFn). The Omit on the options type still blocks queryKey/queryFn,
        // so only the safe defaults are overridable.
        sb.AppendLine("    ...options,");

        sb.AppendLine("  });");
        sb.AppendLine("}");
    }

    /// <summary>
    /// Emits Option A from issues/001-feedback.md: a useState/useEffect/useRef-backed hook
    /// that consumes the AsyncGenerator-style client method and accumulates items into local
    /// state. Aborts on unmount or param change, swallows AbortError, exposes a 4-state
    /// status machine plus cancel/reset controls.
    /// </summary>
    private static void AppendStreamHook(
        StringBuilder sb,
        HookInfo info,
        string segmentCamel,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        var hookName = "use" + info.MethodName.ToPascalCaseForDotNet() + "Stream";
        var segmentProperty = segmentCamel;
        var itemType = string.IsNullOrEmpty(info.ReturnType) ? "unknown" : info.ReturnType;

        // Hook signature: pathParams..., query?, headers?, options?: { enabled?: boolean }
        var hookParams = new List<string>();
        foreach (var param in info.PathParams)
        {
            var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var paramType = TypeScriptOperationHelper.GetParameterType(param, convertDates, brandedIds, info.Path);
            hookParams.Add(paramName + ": " + paramType);
        }

        if (info.QueryParams.Count > 0)
        {
            var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy, convertDates);
            hookParams.Add("query?: " + queryType);
        }

        if (info.HeaderParams.Count > 0)
        {
            var headerType = TypeScriptOperationHelper.BuildHeaderTypeInline(info.HeaderParams, convertDates);
            hookParams.Add("headers?: " + headerType);
        }

        hookParams.Add("options?: { enabled?: boolean }");

        var hookParamStr = string.Join(", ", hookParams);

        // Client call args mirror the streaming client method:
        //   pathParams..., query, headers, controller.signal
        var clientCallParts = new List<string>();
        foreach (var p in info.PathParams)
        {
            clientCallParts.Add((p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
        }

        if (info.QueryParams.Count > 0)
        {
            clientCallParts.Add("query");
        }

        if (info.HeaderParams.Count > 0)
        {
            clientCallParts.Add("headers");
        }

        clientCallParts.Add("controller.signal");
        var clientCallArgs = string.Join(", ", clientCallParts);

        // useEffect dependency list. Path params and the enabled flag are direct deps.
        // Query + headers are non-primitive bags, so we serialize them to a string for a
        // stable dependency value (avoid re-running the effect on every parent re-render).
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
        sb.Append("        for await (const item of api.").Append(segmentProperty).Append('.').Append(info.MethodName).Append('(').Append(clientCallArgs).AppendLine(")) {");
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
    }

    /// <summary>
    /// Emits a useInfiniteQuery hook that consumes the <c>&lt;methodName&gt;Page</c>
    /// companion of a paginated-streaming operation. The hook returns one page at a time
    /// and surfaces <c>fetchNextPage()</c>; the continuation token from the previous page
    /// is threaded back via the <c>x-continuation</c> request header that the Page
    /// companion accepts (see <see cref="TypeScriptClientExtractor"/>).
    /// </summary>
    private static void AppendInfiniteHook(
        StringBuilder sb,
        HookInfo info,
        string segmentCamel,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        var hookName = "use" + info.MethodName.ToPascalCaseForDotNet() + "Infinite";
        var pageMethodName = info.MethodName + "Page";
        var infiniteKeyName = info.MethodName.ToCamelCase().ToTypeScriptIdentifier() + "Infinite";

        // Hook signature: same shape as the streaming sibling minus signal/options —
        // useInfiniteQuery owns the lifecycle, so the consumer just supplies inputs.
        var hookParams = new List<string>();
        foreach (var p in info.PathParams)
        {
            var n = (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var t = TypeScriptOperationHelper.GetParameterType(p, convertDates, brandedIds, info.Path);
            hookParams.Add(n + ": " + t);
        }

        if (info.QueryParams.Count > 0)
        {
            var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy, convertDates);
            hookParams.Add("query?: " + queryType);
        }

        var hookParamStr = string.Join(", ", hookParams);

        // queryKey args mirror the standard query-hook approach: path params + query bag.
        var keyArgs = new List<string>();
        foreach (var p in info.PathParams)
        {
            keyArgs.Add((p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
        }

        if (info.QueryParams.Count > 0)
        {
            keyArgs.Add("query");
        }

        var keyCallArgs = string.Join(", ", keyArgs);

        // Client-side call args for the Page companion: pathParams..., query, headers
        var callArgs = new List<string>();
        foreach (var p in info.PathParams)
        {
            callArgs.Add((p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
        }

        if (info.QueryParams.Count > 0)
        {
            callArgs.Add("query");
        }

        callArgs.Add("pageParam ? { 'x-continuation': pageParam } : undefined");
        var callArgsStr = string.Join(", ", callArgs);

        AppendHookJsDoc(sb, info);
        sb.Append("export function ").Append(hookName).Append('(').Append(hookParamStr).AppendLine(") {");
        sb.AppendLine("  const api = useApiService();");
        sb.AppendLine("  return useInfiniteQuery({");
        sb.Append("    queryKey: ").Append(segmentCamel).Append("Keys.").Append(infiniteKeyName).Append('(').Append(keyCallArgs).AppendLine("),");
        sb.AppendLine("    queryFn: async ({ pageParam }: { pageParam: string | undefined }) => {");
        sb.Append("      const result = await api.").Append(segmentCamel).Append('.').Append(pageMethodName).Append('(').Append(callArgsStr).AppendLine(");");
        sb.AppendLine("      if (result.status === 'ok') {");
        sb.AppendLine("        return result.data;");
        sb.AppendLine("      }");
        sb.AppendLine("      throw new ApiError(");
        sb.AppendLine("        result.response.status,");
        sb.AppendLine("        result.response.statusText,");
        sb.AppendLine("        'error' in result ? result.error.message : 'Request failed',");
        sb.AppendLine("        result.response,");
        sb.AppendLine("      );");
        sb.AppendLine("    },");
        sb.AppendLine("    getNextPageParam: (lastPage) => lastPage.continuationToken ?? undefined,");
        sb.AppendLine("    initialPageParam: undefined as string | undefined,");
        sb.AppendLine("  });");
        sb.AppendLine("}");
    }

    private static void AppendMutationHook(
        StringBuilder sb,
        HookInfo info,
        string segmentCamel,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        var hookName = "use" + info.MethodName.ToPascalCaseForDotNet();
        var segmentProperty = segmentCamel;
        var isVoidReturn = info.ReturnType == "void";
        var isDelete = info.HttpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase);

        // Determine hook signature params (path params that are "stable" go as hook params)
        // and mutation fn arg (body or path params for delete). variablesType tracks the
        // type of the mutationFn arg so we can wire it into UseMutationOptions<TData, TError, TVariables>.
        var hookParams = new List<string>();
        string mutationArg;
        string clientCallArgs;
        string variablesType;

        if (info.HasBody && info.PathParams.Count > 0)
        {
            // Path params as hook params, body as mutation arg
            foreach (var param in info.PathParams)
            {
                var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
                var paramType = TypeScriptOperationHelper.GetParameterType(param, convertDates, brandedIds, info.Path);
                hookParams.Add(paramName + ": " + paramType);
            }

            mutationArg = "(body: " + info.BodyType + ")";
            clientCallArgs = BuildClientCallArgs(info.PathParams, info.QueryParams, info.HeaderParams, hasBody: true, namingStrategy: namingStrategy);
            variablesType = info.BodyType;
        }
        else if (info.HasBody)
        {
            // Body only as mutation arg
            mutationArg = "(body: " + info.BodyType + ")";
            clientCallArgs = BuildClientCallArgs(info.PathParams, info.QueryParams, info.HeaderParams, hasBody: true, namingStrategy: namingStrategy);
            variablesType = info.BodyType;
        }
        else if (info.HasFileUploadArg && info.PathParams.Count > 0)
        {
            // File upload with path params — merge path params into mutation arg
            var pathParamParts = new List<string>();
            var pathParamNames = new List<string>();
            foreach (var param in info.PathParams)
            {
                var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
                var paramType = TypeScriptOperationHelper.GetParameterType(param, convertDates, brandedIds, info.Path);
                pathParamParts.Add(paramName + ": " + paramType);
                pathParamNames.Add(paramName);
            }

            // Parse the existing file upload param declaration to extract its type
            var colonIdx = info.FileUploadParam.IndexOf(':', StringComparison.Ordinal);
            var fileArgName = info.FileUploadParam[..colonIdx].Trim();
            var fileTypeStr = info.FileUploadParam[(colonIdx + 1)..].Trim();

            // Build destructured param: ({ pizzaId, ...data }: { pizzaId: string; file?: ...; altText?: ... })
            string mutationParamDestructure;
            string mutationParamType;

            if (fileTypeStr.StartsWith("{", StringComparison.Ordinal) && fileTypeStr.EndsWith("}", StringComparison.Ordinal))
            {
                // Inline object type — merge path params into it
                var innerProps = fileTypeStr[1..^1].Trim();
                mutationParamType = "{ " + string.Join("; ", pathParamParts) + "; " + innerProps + " }";
                mutationParamDestructure = "{ " + string.Join(", ", pathParamNames) + ", ..." + fileArgName + " }";
            }
            else
            {
                // Non-object type (e.g., Blob | File) — wrap in object with path params
                mutationParamType = "{ " + string.Join("; ", pathParamParts) + "; " + info.FileUploadParam + " }";
                mutationParamDestructure = "{ " + string.Join(", ", pathParamNames) + ", " + fileArgName + " }";
            }

            mutationArg = "(" + mutationParamDestructure + ": " + mutationParamType + ")";
            clientCallArgs = string.Join(", ", pathParamNames) + ", " + fileArgName;
            variablesType = mutationParamType;
        }
        else if (info.HasFileUploadArg)
        {
            // File upload without path params
            mutationArg = "(" + info.FileUploadParam + ")";
            clientCallArgs = info.FileUploadArgName;

            // info.FileUploadParam is `<name>: <type>` — slice the type out so it can
            // appear inside the UseMutationOptions generic.
            var fileColon = info.FileUploadParam.IndexOf(':', StringComparison.Ordinal);
            variablesType = fileColon >= 0
                ? info.FileUploadParam[(fileColon + 1)..].Trim()
                : "unknown";
        }
        else if (info.PathParams.Count > 0)
        {
            // Path params as mutation args (e.g., delete by ID)
            if (info.PathParams.Count == 1)
            {
                var param = info.PathParams[0];
                var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
                var paramType = TypeScriptOperationHelper.GetParameterType(param, convertDates, brandedIds, info.Path);
                mutationArg = "(" + paramName + ": " + paramType + ")";
                clientCallArgs = paramName;
                variablesType = paramType;
            }
            else
            {
                var paramParts = info.PathParams.Select(p =>
                {
                    var pName = (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
                    var pType = TypeScriptOperationHelper.GetParameterType(p, convertDates, brandedIds, info.Path);
                    return pName + ": " + pType;
                }).ToList();
                mutationArg = "(params: { " + string.Join("; ", paramParts) + " })";
                clientCallArgs = string.Join(
                    ", ",
                    info.PathParams.Select(p => "params." + (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy)));
                variablesType = "{ " + string.Join("; ", paramParts) + " }";
            }
        }
        else
        {
            // No args
            mutationArg = "()";
            clientCallArgs = string.Empty;
            variablesType = "void";
        }

        // Query params are hook-scoped (set once per useXxx() call) for mutations,
        // matching the pattern used for header params below. HasBody branches already
        // include 'query' in clientCallArgs via BuildClientCallArgs; remaining branches
        // (path-only, file-upload, no-args) need it appended here as well.
        // Not adding query to hookParams was the cause of TS2304 ("Cannot find name 'query'").
        if (info.QueryParams.Count > 0)
        {
            var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy, convertDates);
            hookParams.Add("query?: " + queryType);

            if (!info.HasBody)
            {
                clientCallArgs = clientCallArgs.Length == 0 ? "query" : clientCallArgs + ", query";
            }
        }

        // Header params are hook-scoped (set once per useXxx() call) for mutations,
        // matching how query params behave for the same operation. The order at the
        // call site is path-params..., body, query, headers — see BuildClientCallArgs.
        if (info.HeaderParams.Count > 0)
        {
            var headerType = TypeScriptOperationHelper.BuildHeaderTypeInline(info.HeaderParams, convertDates);
            hookParams.Add("headers?: " + headerType);
            clientCallArgs = clientCallArgs.Length == 0 ? "headers" : clientCallArgs + ", headers";
        }

        // Pass-through options for the mutation. TData mirrors the unwrap path: void
        // for delete / no-body returns, the operation's return type otherwise.
        var mutationDataType = (isVoidReturn || isDelete) ? "void" : info.ReturnType;
        hookParams.Add("options?: Omit<UseMutationOptions<" + mutationDataType + ", ApiError, " + variablesType + ">, 'mutationFn'>");

        var hookParamStr = string.Join(", ", hookParams);

        AppendHookJsDoc(sb, info);
        sb.Append("export function ").Append(hookName).Append('(').Append(hookParamStr).AppendLine(") {");
        sb.AppendLine("  const api = useApiService();");
        sb.AppendLine("  const queryClient = useQueryClient();");
        sb.AppendLine("  return useMutation({");
        sb.Append("    mutationFn: async ").Append(mutationArg).AppendLine(" => {");
        sb.Append("      const result = await api.").Append(segmentProperty).Append('.').Append(info.MethodName).Append('(').Append(clientCallArgs).AppendLine(");");

        AppendResultUnwrap(sb, info.ReturnType, info.HttpMethod, info.Success2xxDiscriminators);

        sb.AppendLine("    },");

        // Spread caller options FIRST so the composed onSuccess below (which forwards to
        // the caller's onSuccess after invalidating) wins over a bare ...options.onSuccess.
        // Other handlers (onError, onSettled, retry, …) still pass through via the spread.
        sb.AppendLine("    ...options,");

        // React Query 5.81 widened the mutation lifecycle callbacks to 4 params —
        // onSuccess(data, variables, onMutateResult, context). Forward all four so the
        // composed wrapper type-matches the caller's options.onSuccess. See the bumped
        // peerDependency (@tanstack/react-query >= 5.81) in the package scaffold.
        sb.AppendLine("    onSuccess: (data, variables, onMutateResult, context) => {");
        sb.Append("      queryClient.invalidateQueries({ queryKey: ").Append(segmentCamel).AppendLine("Keys.all });");
        sb.AppendLine("      options?.onSuccess?.(data, variables, onMutateResult, context);");
        sb.AppendLine("    },");

        sb.AppendLine("  });");
        sb.AppendLine("}");
    }

    private static void AppendResultUnwrap(
        StringBuilder sb,
        string returnType,
        string httpMethod,
        List<string> success2xxDiscriminators)
    {
        var isVoid = returnType == "void";
        var isDelete = httpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase);

        // The narrowing list must match the per-op result type's declared 2xx arms. Without
        // this, a hook generated for an op that only declares 200 would type-error on
        // `result.status === 'created'` because 'created' isn't in the per-op union.
        if (isVoid || isDelete)
        {
            var allSuccess = success2xxDiscriminators.Count > 0
                ? success2xxDiscriminators
                : new List<string> { "noContent", "ok" };
            sb.Append("      if (").Append(BuildStatusDisjunction(allSuccess)).AppendLine(") {");
            sb.AppendLine("        return;");
        }
        else
        {
            // Data-bearing arms only — noContent has no data to forward.
            var dataBearing = success2xxDiscriminators
                .Where(d => !string.Equals(d, "noContent", StringComparison.Ordinal))
                .ToList();
            if (dataBearing.Count == 0)
            {
                dataBearing.Add("ok");
            }

            sb.Append("      if (").Append(BuildStatusDisjunction(dataBearing)).AppendLine(") {");
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

    private static string BuildStatusDisjunction(List<string> discriminators)
        => string.Join(
            " || ",
            discriminators.Select(d => $"result.status === '{d}'"));

    private static string BuildClientCallArgs(
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        List<OpenApiParameter> headerParams,
        bool hasBody,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase)
    {
        // Argument order must mirror the client method's parameter list:
        //   pathParams..., body, query, headers
        // See TypeScriptClientExtractor.BuildParameterList.
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

        if (headerParams.Count > 0)
        {
            args.Add("headers");
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

        // Check if this is a "get...By..." pattern.
        // Derive a unique key by stripping the "get" prefix and the segment name so that
        // multiple get*By* operations on the same segment each get a distinct key —
        // e.g. getFoundryUsagesByAccount → usagesByAccount, getFoundryUsagesByModel → usagesByModel.
        // Returning the hardcoded "detail" caused TS1117 (duplicate object-literal keys)
        // when a segment had more than one get*By* operation.
        if (methodName.StartsWith("get", StringComparison.OrdinalIgnoreCase) &&
            methodName.Contains("By", StringComparison.Ordinal))
        {
            var withoutGet = methodName.Length > 3 ? methodName[3..] : methodName;
            if (withoutGet.StartsWith(segmentPascal, StringComparison.OrdinalIgnoreCase) &&
                withoutGet.Length > segmentPascal.Length)
            {
                withoutGet = withoutGet[segmentPascal.Length..];
            }

            return withoutGet.Length > 0
                ? char.ToLowerInvariant(withoutGet[0]) + withoutGet[1..]
                : methodName;
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
        TypeScriptNamingStrategy namingStrategy,
        HashSet<string> writableSchemas)
    {
        var isStreaming = operation.IsStreamingResponse();
        var isFileDownload = operation.HasFileDownload();
        var isFileUpload = operation.HasFileUpload();
        var operationId = operation.GetOperationId(path, httpMethod);
        var methodName = operationId.ToCamelCase().ToTypeScriptIdentifier();

        var pathParams = TypeScriptOperationHelper.GetMergedParameters(
            operation, openApiDoc, path, ParameterLocation.Path);
        var queryParams = TypeScriptOperationHelper.GetMergedParameters(
            operation, openApiDoc, path, ParameterLocation.Query);
        var headerParams = TypeScriptOperationHelper.GetMergedParameters(
            operation, openApiDoc, path, ParameterLocation.Header);

        var returnType = TypeScriptOperationHelper.GetReturnType(operation, isStreaming, isFileDownload);

        var (bodySchema, bodyContentType) = operation.GetRequestBodySchemaWithContentType();
        var hasBody = bodySchema != null && !isFileUpload;
        var bodyType = hasBody ? bodySchema!.ToTypeScriptReturnType() : string.Empty;

        // Mirror the client extractor: a body that's a $ref to a schema with the
        // readOnly/writeOnly split must use the <Name>Writable variant in the mutation
        // signature so consumers can't accidentally pass readOnly fields.
        if (hasBody && bodySchema is OpenApiSchemaReference bodyRef)
        {
            var bodyRefName = bodyRef.Reference.Id ?? bodyRef.Id;
            if (bodyRefName != null && writableSchemas.Contains(bodyRefName))
            {
                bodyType = bodyRefName + TypeScriptModelExtractor.WritableSuffix;
            }
        }

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

        // Paginated-streaming ops get a useInfiniteQuery sibling. PageReturnType is
        // the full response schema (PaginationResult<Item>) — i.e. the schema treated as
        // non-streaming — so the queryFn's data type lines up with the Page companion.
        var isPaginatedStreaming = operation.IsPaginatedStreamingOperation();
        var pageReturnType = isPaginatedStreaming
            ? TypeScriptOperationHelper.GetReturnType(operation, isStreaming: false, isFileDownload: false)
            : string.Empty;

        return new HookInfo(
            MethodName: methodName,
            HttpMethod: httpMethod,
            IsQuery: isQuery,
            IsStreaming: isStreaming,
            PathParams: pathParams,
            QueryParams: queryParams,
            HeaderParams: headerParams,
            ReturnType: returnType,
            HasBody: hasBody,
            BodyType: bodyType,
            HasFileUploadArg: hasFileUploadArg,
            FileUploadParam: fileUploadParam,
            FileUploadArgName: fileUploadArgName,
            Success2xxDiscriminators: TypeScriptOperationHelper.CollectDeclared2xxDiscriminators(operation),
            IsPaginatedStreaming: isPaginatedStreaming,
            PageReturnType: pageReturnType,
            Summary: operation.Summary,
            Description: operation.Description,
            Deprecated: operation.Deprecated,
            Path: path);
    }

    /// <summary>
    /// Renders a `/** ... */` block from the operation's summary/description plus a
    /// `@deprecated` tag when the spec marks the operation deprecated. Skipped when
    /// there's nothing worth saying so the emitted file stays clean.
    /// </summary>
    private static void AppendHookJsDoc(
        StringBuilder sb,
        HookInfo info)
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
        bool IsStreaming,
        List<OpenApiParameter> PathParams,
        List<OpenApiParameter> QueryParams,
        List<OpenApiParameter> HeaderParams,
        string ReturnType,
        bool HasBody,
        string BodyType,
        bool HasFileUploadArg,
        string FileUploadParam,
        string FileUploadArgName,
        List<string> Success2xxDiscriminators,
        bool IsPaginatedStreaming,
        string PageReturnType,
        string? Summary,
        string? Description,
        bool Deprecated,
        string Path);
}