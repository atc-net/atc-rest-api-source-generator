// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable InvertIf
// ReSharper disable InconsistentNaming
namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

using static Atc.Rest.Api.Generator.Cli.Extractors.TypeScript.TypeScriptHookModel;

/// <summary>
/// Generates per-segment TanStack Query (React Query) hook files from OpenAPI operations.
/// GET operations become useQuery hooks; POST/PUT/PATCH/DELETE become useMutation hooks.
/// Streaming operations (x-return-async-enumerable) become useXxxStream hooks backed by
/// useState + useEffect + AbortController.
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
            // A hook for the safe metadata verbs has no meaning - and as a mutation it would invalidate the queries.
            if (method.ToUpperInvariant() is "HEAD" or "OPTIONS" or "TRACE")
            {
                continue;
            }

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

                    var brand = TypeScriptBrandedIdExtractor.ResolveParamBrand(info.Path, param.Name, param.Schema);
                    if (brand is not null)
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
                var refName = bodyRef.ComponentName;
                if (refName is not null && writableSchemas.Contains(refName))
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

        // Paginated reads get a useInfiniteQuery sibling of their query hook (in every hooks mode).
        var needsUseInfiniteQuery = hookInfos.Any(h => h.Pagination is not null);

        // Build the body (query-key factory + hooks) BEFORE the imports so model/enum
        // import lines can be narrowed to identifiers the body actually references. The
        // schema-graph scan can over-collect — e.g. an enum reachable only through an
        // inline request body, or an array-alias wrapper for a streamed item type — which
        // would otherwise fail tsc under noUnusedLocals.
        var bodySb = new StringBuilder();

        // Write query key factory
        var segmentCamel = segment.ToCamelCase();
        hookInfos = AssignKeyNames(hookInfos, segmentCamel);
        AppendQueryKeyFactory(bodySb, segmentCamel, hookInfos, namingStrategy, convertDates, brandedIds);
        AppendQueryOptionsFactory(bodySb, segmentCamel, hookInfos, namingStrategy, convertDates, brandedIds);

        // Write hook functions
        foreach (var info in hookInfos)
        {
            if (info.IsStreaming)
            {
                bodySb.AppendLine();
                AppendStreamHook(bodySb, info, segmentCamel, namingStrategy, convertDates, brandedIds);
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

                if (info.Pagination is not null)
                {
                    bodySb.AppendLine();
                    AppendInfiniteHook(bodySb, info, segmentCamel, namingStrategy, convertDates, brandedIds);
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
        sb.Append(headerContent);

        // Write imports (narrowed to what the body references)
        var hookImports = new HookImports(
            UseQuery: needsUseQuery,
            UseSuspenseQuery: needsUseSuspenseQuery,
            UseMutation: needsUseMutation,
            UseQueryClient: needsUseQueryClient,
            ReactHooks: needsReactHooks,
            ApiError: needsApiError,
            UseInfiniteQuery: needsUseInfiniteQuery,
            QueryOptions: hookInfos.Any(h => !h.IsStreaming && h.IsQuery));
        AppendImports(sb, importTypes, enumNames, hookImports, brandImports, bodyText);

        sb.Append(bodyText);

        return sb.ToString();
    }

    private static void AppendImports(
        StringBuilder sb,
        HashSet<string> importTypes,
        HashSet<string>? enumNames,
        HookImports hookImports,
        SortedSet<string> brandImports,
        string bodyText)
    {
        var needsUseQuery = hookImports.UseQuery;
        var needsUseSuspenseQuery = hookImports.UseSuspenseQuery;
        var needsUseMutation = hookImports.UseMutation;
        var needsUseQueryClient = hookImports.UseQueryClient;
        var needsReactHooks = hookImports.ReactHooks;
        var needsApiError = hookImports.ApiError;
        var needsUseInfiniteQuery = hookImports.UseInfiniteQuery;
        var needsQueryOptions = hookImports.QueryOptions;

        // React primitive hooks (only required by stream hooks)
        if (needsReactHooks)
        {
            sb.AppendLine("import { useCallback, useEffect, useRef, useState } from 'react';");
        }

        // TanStack Query imports: the <segment>Queries factory first, then the hooks
        var queryImports = new List<string>();
        if (needsQueryOptions)
        {
            queryImports.Add("queryOptions");
        }

        if (needsUseInfiniteQuery)
        {
            queryImports.Add("infiniteQueryOptions");
        }

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

        if (needsUseInfiniteQuery)
        {
            optionImports.Add("InfiniteData");
            optionImports.Add("UseInfiniteQueryOptions");
        }

        if (optionImports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", optionImports)).AppendLine(" } from '@tanstack/react-query';");
        }

        sb.AppendLine("import { useApiService } from './useApiService';");
        if (needsQueryOptions)
        {
            // The factory takes the service, so a loader or server code can use it without the provider
            sb.AppendLine("import type { ApiService } from '../client/ApiService';");
        }

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

        if (brandImports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", brandImports)).AppendLine(" } from '../types/BrandedIds';");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Emits the <c>&lt;segment&gt;Queries</c> factory: the TanStack <c>queryOptions</c> (and, for a paginated read,
    /// <c>infiniteQueryOptions</c>) of every read, shared by the hooks and by <c>queryClient.prefetchQuery</c> /
    /// <c>ensureQueryData</c> in a route loader or on the server. The queryFn hands React Query's signal to the
    /// client, so a query React Query cancels (unused, superseded, <c>cancelQueries</c>) stops its request.
    /// </summary>
    private static void AppendQueryOptionsFactory(
        StringBuilder sb,
        string segmentCamel,
        List<HookInfo> hookInfos,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        var queries = hookInfos.Where(h => !h.IsStreaming && h.IsQuery).ToList();
        if (queries.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("/**");
        sb.AppendLine(" * The query options of each read: spread into useQuery or useSuspenseQuery, or pass to");
        sb.AppendLine(" * queryClient.prefetchQuery / ensureQueryData (a route loader, server rendering). The query hands");
        sb.AppendLine(" * React Query's signal to the client, so a cancelled or unused query stops its request.");
        sb.AppendLine(" */");
        sb.Append("const ").Append(segmentCamel).AppendLine("Queries = {");

        foreach (var info in queries)
        {
            AppendQueryOptionsEntry(sb, info, segmentCamel, namingStrategy, convertDates, brandedIds);
            if (info.Pagination is not null)
            {
                AppendInfiniteQueryOptionsEntry(sb, info, segmentCamel, namingStrategy, convertDates, brandedIds);
            }
        }

        sb.AppendLine("};");
        sb.AppendLine();
        sb.Append("export { ").Append(segmentCamel).AppendLine("Queries };");
    }

    private static void AppendQueryOptionsEntry(
        StringBuilder sb,
        HookInfo info,
        string segmentCamel,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        var (declarations, names) = QueryInputs(info, info.QueryParams, info.HeaderParams, namingStrategy, convertDates, brandedIds);

        // The key: headers and cookies are left out so the cache does not fragment on per-request headers
        // (correlation ids), unless a header is part of what the read returns (KeyHeaders).
        var keyArgs = new List<string>();
        foreach (var p in info.PathParams)
        {
            keyArgs.Add(TypeScriptOperationHelper.ParameterIdentifier(p, namingStrategy));
        }

        if (info.HasBody)
        {
            keyArgs.Add("body");
        }

        foreach (var p in info.QuerystringParams)
        {
            keyArgs.Add(TypeScriptOperationHelper.ParameterIdentifier(p, namingStrategy));
        }

        if (info.QueryParams.Count > 0)
        {
            keyArgs.Add("query");
        }

        if (KeyHeaders(info, info.HeaderParams).Count > 0)
        {
            keyArgs.Add("headers");
        }

        var signalName = TypeScriptOperationHelper.SignalParameterName(names);
        var clientCallArgs = WithSignal(BuildClientCallArgs(info.PathParams, info.QueryParams, info.HeaderParams, info.CookieParams, info.QuerystringParams, hasBody: info.HasBody, namingStrategy: namingStrategy), signalName);
        var dataType = info.ReturnType == "void" ? "void" : info.ReturnType;

        sb.Append("  ").Append(info.MethodName).Append(": (").Append(TypeScriptOperationHelper.JoinParameters(["api: ApiService", .. declarations]))
            .Append(") => queryOptions<").Append(dataType).AppendLine(", ApiError>({");
        sb.Append("    queryKey: ").Append(segmentCamel).Append("Keys.").Append(KeyNameOf(info, segmentCamel)).Append('(').Append(string.Join(", ", keyArgs)).AppendLine("),");
        sb.Append("    queryFn: async ({ ").Append(SignalBinding(signalName)).AppendLine(" }) => {");
        sb.Append("      const result = await api.").Append(segmentCamel).Append('.').Append(info.MethodName).Append('(').Append(clientCallArgs).AppendLine(");");
        AppendResultUnwrap(sb, info.ReturnType, info.HttpMethod, info.Success2xxDiscriminators);
        sb.AppendLine("    },");

        AppendNoRetry(sb, info);
        sb.AppendLine("  }),");
    }

    // A specification that declares retry (x-retry-*) has the ApiClient retry per operation: React Query's own retry
    // would multiply those attempts and re-send operations marked x-retry-enabled: false. One that declares none
    // leaves retry to the app (its QueryClient defaults), as React Query does
    private static void AppendNoRetry(
        StringBuilder sb,
        HookInfo info)
    {
        if (info.RetryOwnedByClient)
        {
            sb.AppendLine("    retry: false,");
        }
    }

    private static void AppendInfiniteQueryOptionsEntry(
        StringBuilder sb,
        HookInfo info,
        string segmentCamel,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        var pagination = info.Pagination!;
        var (queryParams, headerParams) = InfiniteHookParameters(info);
        var (declarations, names) = QueryInputs(info, queryParams, headerParams, namingStrategy, convertDates, brandedIds);

        var keyArgs = new List<string>();
        foreach (var p in info.PathParams)
        {
            keyArgs.Add(TypeScriptOperationHelper.ParameterIdentifier(p, namingStrategy));
        }

        if (info.HasBody)
        {
            keyArgs.Add("body");
        }

        foreach (var p in info.QuerystringParams)
        {
            keyArgs.Add(TypeScriptOperationHelper.ParameterIdentifier(p, namingStrategy));
        }

        if (queryParams.Count > 0)
        {
            keyArgs.Add("query");
        }

        if (KeyHeaders(info, headerParams).Count > 0)
        {
            keyArgs.Add("headers");
        }

        var signalName = TypeScriptOperationHelper.SignalParameterName(names);
        var pageType = info.ReturnType;
        sb.Append("  ").Append(info.MethodName).Append("Infinite: (").Append(TypeScriptOperationHelper.JoinParameters(["api: ApiService", .. declarations]))
            .Append(") => infiniteQueryOptions<").Append(pageType).Append(", ApiError, InfiniteData<").Append(pageType)
            .AppendLine(", string | undefined>, readonly unknown[], string | undefined>({");
        sb.Append("    queryKey: ").Append(segmentCamel).Append("Keys.").Append(KeyNameOf(info, segmentCamel)).Append("Infinite(").Append(string.Join(", ", keyArgs)).AppendLine("),");
        sb.Append("    queryFn: async ({ pageParam, ").Append(SignalBinding(signalName)).AppendLine(" }: { pageParam: string | undefined; signal: AbortSignal }) => {");
        sb.Append("      const result = await api.").Append(segmentCamel).Append('.').Append(info.MethodName).Append('(').Append(WithSignal(BuildInfiniteClientCallArgs(info, "pageParam", namingStrategy), signalName)).AppendLine(");");
        AppendResultUnwrap(sb, info.ReturnType, info.HttpMethod, info.Success2xxDiscriminators);
        sb.AppendLine("    },");
        sb.AppendLine("    initialPageParam: undefined as string | undefined,");

        // A token equal to the one this page was fetched with ends the query (a server that repeats its token
        // would page forever)
        sb.Append("    getNextPageParam: (lastPage, _allPages, lastPageParam) => { const next = ").Append(TokenAccess("lastPage", pagination, namingStrategy)).AppendLine(" || undefined; return next !== lastPageParam ? next : undefined; },");

        AppendNoRetry(sb, info);
        sb.AppendLine("  }),");
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

        // Mirrors the client method's parameter order: pathParams..., body, querystring..., query, headers, cookies.
        var (hookParams, names) = QueryInputs(info, info.QueryParams, info.HeaderParams, namingStrategy, convertDates, brandedIds);

        // Pass-through options: every generated hook accepts a final options? arg whose
        // type omits the keys the hook body already populates. Consumers can override
        // staleTime, gcTime, select, refetchOnWindowFocus, etc. without wrapping. The
        // options type follows the underlying query function — UseQueryOptions for
        // useQuery, UseSuspenseQueryOptions for useSuspenseQuery (suspense flavor has a
        // different shape: no 'enabled', stricter on initialData, etc.).
        var queryDataType = info.ReturnType == "void" ? "void" : info.ReturnType;
        hookParams.Add("options?: Omit<" + optionsType + "<" + queryDataType + ", ApiError>, 'queryKey' | 'queryFn'>");

        AppendHookJsDoc(sb, info);
        sb.Append("export function ").Append(hookName).Append('(').Append(TypeScriptOperationHelper.JoinParameters(hookParams)).AppendLine(") {");
        sb.AppendLine("  const api = useApiService();");
        sb.Append("  return ").Append(queryFn).AppendLine("({");
        sb.Append("    ...").Append(segmentCamel).Append("Queries.").Append(info.MethodName).Append("(api").Append(string.Concat(names.Select(n => ", " + n))).AppendLine("),");

        // Add enabled guard for detail queries with path params — but only for the
        // standard variant. useSuspenseQuery doesn't support conditional execution
        // (it throws a promise that the boundary catches), so consumers control whether
        // the hook runs by mounting/unmounting the component, not by toggling enabled.
        // A body-carrying read (QUERY) guards on the body for the same reason a detail query
        // guards on its id: the caller often has nothing to send on the first render. An
        // operation with both — a QUERY nested under a path parameter — guards on both, since
        // either one being absent makes the request meaningless.
        // Spread caller options so they override the generated defaults (retry); the guard below keeps the
        // caller's enabled as a narrowing. The Omit on the options type still blocks queryKey/queryFn.
        sb.AppendLine("    ...options,");
        if (!isSuspense)
        {
            AppendEnabledGuard(sb, info, namingStrategy);
        }

        sb.AppendLine("  });");
        sb.AppendLine("}");
    }

    /// <summary>
    /// Emits a useInfiniteQuery hook for a paginated read: the query hook's inputs without the
    /// continuation parameter; each page sends the previous page's token through that parameter,
    /// and the pages end at the first page without a token.
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
        var (queryParams, headerParams) = InfiniteHookParameters(info);
        var (hookParams, names) = QueryInputs(info, queryParams, headerParams, namingStrategy, convertDates, brandedIds);

        // The same override point as the query hooks: everything but what the hook computes itself.
        var pageType = info.ReturnType;
        hookParams.Add(
            "options?: Omit<UseInfiniteQueryOptions<" + pageType + ", ApiError, InfiniteData<" + pageType +
            ", string | undefined>, readonly unknown[], string | undefined>, 'queryKey' | 'queryFn' | 'initialPageParam' | 'getNextPageParam'>");

        AppendHookJsDoc(sb, info);
        sb.Append("export function ").Append(hookName).Append('(').Append(TypeScriptOperationHelper.JoinParameters(hookParams)).AppendLine(") {");
        sb.AppendLine("  const api = useApiService();");
        sb.AppendLine("  return useInfiniteQuery({");
        sb.Append("    ...").Append(segmentCamel).Append("Queries.").Append(info.MethodName).Append("Infinite(api").Append(string.Concat(names.Select(n => ", " + n))).AppendLine("),");

        // No request while a path parameter or the body of a QUERY read is missing (as the query hook).
        sb.AppendLine("    ...options,");
        AppendEnabledGuard(sb, info, namingStrategy);
        sb.AppendLine("  });");
        sb.AppendLine("}");
    }

    /// <summary>
    /// The inputs of a read, in the client method's order (path parameters, body, querystring parameters,
    /// query, headers, cookies): their declarations for a signature and their names for a call.
    /// </summary>
    private static (List<string> Declarations, List<string> Names) QueryInputs(
        HookInfo info,
        List<OpenApiParameter> queryParams,
        List<OpenApiParameter> headerParams,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        var declarations = new List<string>();
        var names = new List<string>();
        foreach (var param in info.PathParams)
        {
            var name = TypeScriptOperationHelper.ParameterIdentifier(param, namingStrategy);
            declarations.Add(name + ": " + TypeScriptOperationHelper.GetParameterType(param, convertDates, brandedIds, info.Path));
            names.Add(name);
        }

        if (info.HasBody)
        {
            declarations.Add("body: " + info.BodyType);
            names.Add("body");
        }

        foreach (var param in info.QuerystringParams)
        {
            var name = TypeScriptOperationHelper.ParameterIdentifier(param, namingStrategy);
            declarations.Add(name + "?: string");
            names.Add(name);
        }

        if (queryParams.Count > 0)
        {
            declarations.Add(TypeScriptOperationHelper.BuildQueryParameter(queryParams, namingStrategy, convertDates));
            names.Add("query");
        }

        if (headerParams.Count > 0)
        {
            declarations.Add(TypeScriptOperationHelper.BuildHeaderParameter(headerParams, convertDates));
            names.Add("headers");
        }

        if (info.CookieParams.Count > 0)
        {
            declarations.Add("cookies?: " + BuildCookieTypeInline(info.CookieParams, namingStrategy));
            names.Add("cookies");
        }

        return (declarations, names);
    }

    /// <summary>
    /// <c>enabled</c> for a read that has nothing to send yet: a missing path parameter or the body of a QUERY.
    /// </summary>
    private static void AppendEnabledGuard(
        StringBuilder sb,
        HookInfo info,
        TypeScriptNamingStrategy namingStrategy)
    {
        // Every path parameter, by presence (an id of 0 is an id); the caller's enabled narrows the guard, it
        // does not replace it - so this comes after the options spread
        var guards = info.PathParams
            .Select(p => TypeScriptOperationHelper.ParameterIdentifier(p, namingStrategy) + " != null")
            .ToList();
        if (info.HasBody)
        {
            guards.Add("body != null");
        }

        if (guards.Count > 0)
        {
            sb.Append("    enabled: ").Append(string.Join(" && ", guards)).AppendLine(" && (options?.enabled ?? true),");
        }
    }

    private static string WithSignal(
        string arguments,
        string signalName)
        => arguments.Length == 0 ? signalName : arguments + ", " + signalName;

    private static string SignalBinding(string signalName)
        => signalName == "signal" ? "signal" : "signal: " + signalName;

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

        // Hook-scoped params, the mutationFn argument and its variables type (shared with the SWR hooks).
        var signature = BuildMutationSignature(info, namingStrategy, convertDates, brandedIds);
        var hookParams = signature.HookParams;
        var mutationArg = signature.MutationArg;
        var clientCallArgs = signature.ClientCallArgs;
        var variablesType = signature.VariablesType;

        // Pass-through options for the mutation. TData mirrors the unwrap path: void
        // for delete / no-body returns, the operation's return type otherwise.
        var mutationDataType = (isVoidReturn || isDelete) ? "void" : info.ReturnType;
        hookParams.Add("options?: Omit<UseMutationOptions<" + mutationDataType + ", ApiError, " + variablesType + ">, 'mutationFn'>");

        var hookParamStr = TypeScriptOperationHelper.JoinParameters(hookParams);

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
        // Awaited, so mutateAsync resolves after the invalidated queries have refetched
        sb.AppendLine("    onSuccess: async (data, variables, onMutateResult, context) => {");
        sb.Append("      await queryClient.invalidateQueries({ queryKey: ").Append(segmentCamel).AppendLine("Keys.all });");
        sb.AppendLine("      await options?.onSuccess?.(data, variables, onMutateResult, context);");
        sb.AppendLine("    },");

        sb.AppendLine("  });");
        sb.AppendLine("}");
    }

    /// <summary>
    /// What a hooks file imports from React and TanStack Query, and whether it needs ApiError.
    /// </summary>
    /// <param name="UseQuery">The useQuery hooks (and their options type).</param>
    /// <param name="UseSuspenseQuery">The useSuspenseQuery hooks (and their options type).</param>
    /// <param name="UseMutation">The useMutation hooks (and their options type).</param>
    /// <param name="UseQueryClient">useQueryClient (a mutation invalidates the reads).</param>
    /// <param name="ReactHooks">The React hooks a stream hook is written with.</param>
    /// <param name="ApiError">ApiError (the result unwrap of a query or a mutation).</param>
    /// <param name="UseInfiniteQuery">The useInfiniteQuery hooks of the paginated reads.</param>
    /// <param name="QueryOptions">The queryOptions factory of the reads.</param>
    private readonly record struct HookImports(
        bool UseQuery,
        bool UseSuspenseQuery,
        bool UseMutation,
        bool UseQueryClient,
        bool ReactHooks,
        bool ApiError,
        bool UseInfiniteQuery,
        bool QueryOptions);
}