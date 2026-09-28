// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable InvertIf
// ReSharper disable InconsistentNaming
namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

using static Atc.Rest.Api.Generator.Cli.Extractors.TypeScript.TypeScriptHookModel;

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
                var refName = bodyRef.Reference.Id ?? bodyRef.Id;
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
        if (headerContent is not null)
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

        // Mirrors the client method's parameter order: pathParams..., body, querystring..., query.
        if (info.HasBody)
        {
            hookParams.Add("body: " + info.BodyType);
        }

        foreach (var param in info.QuerystringParams)
        {
            var paramName = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            hookParams.Add(paramName + "?: string");
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

        if (info.CookieParams.Count > 0)
        {
            var cookieType = BuildCookieTypeInline(info.CookieParams, namingStrategy);
            hookParams.Add("cookies?: " + cookieType);
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

        // Build key args — headers and cookies are intentionally excluded so the React Query
        // cache does NOT fragment on per-request headers/cookies (correlation IDs, etc).
        // Query params and querystring params ARE included alongside path params.
        var keyArgsParts = new List<string>();
        foreach (var p in info.PathParams)
        {
            keyArgsParts.Add((p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
        }

        if (info.HasBody)
        {
            keyArgsParts.Add("body");
        }

        foreach (var p in info.QuerystringParams)
        {
            keyArgsParts.Add((p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
        }

        if (info.QueryParams.Count > 0)
        {
            keyArgsParts.Add("query");
        }

        var keyCallArgs = string.Join(", ", keyArgsParts);

        // Build client call args (headers and cookies ARE forwarded to the client method)
        var clientCallArgs = BuildClientCallArgs(info.PathParams, info.QueryParams, info.HeaderParams, info.CookieParams, info.QuerystringParams, hasBody: info.HasBody, namingStrategy: namingStrategy);

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
        // A body-carrying read (QUERY) guards on the body for the same reason a detail query
        // guards on its id: the caller often has nothing to send on the first render. An
        // operation with both — a QUERY nested under a path parameter — guards on both, since
        // either one being absent makes the request meaningless.
        if (!isSuspense)
        {
            var guards = new List<string>();
            if (info.PathParams.Count > 0)
            {
                guards.Add("!!" + (info.PathParams[0].Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
            }

            if (info.HasBody)
            {
                guards.Add("!!body");
            }

            if (guards.Count > 0)
            {
                sb.Append("    enabled: ").Append(string.Join(" && ", guards)).AppendLine(",");
            }
        }

        // The ApiClient already retries per operation as the spec says (x-retry-*); React Query's own
        // retry would multiply those attempts and re-send operations marked x-retry-enabled: false.
        sb.AppendLine("    retry: false,");

        // Spread caller options LAST so they override the generated defaults (enabled, retry,
        // queryKey, queryFn). The Omit on the options type still blocks queryKey/queryFn,
        // so only the safe defaults are overridable.
        sb.AppendLine("    ...options,");

        sb.AppendLine("  });");
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

        // Re-throw the client's own ApiError (a ValidationError keeps its errors; body, detail and
        // title stay available); only non-ApiError arms (parseError, schemaMismatch) are wrapped.
        sb.AppendLine("      if ('error' in result && result.error instanceof ApiError) {");
        sb.AppendLine("        throw result.error;");
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

        // The ApiClient owns retry (x-retry-*); React Query's own retry would multiply the attempts.
        sb.AppendLine("    retry: false,");
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
}