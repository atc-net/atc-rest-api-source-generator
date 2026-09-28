// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

using static Atc.Rest.Api.Generator.Cli.Extractors.TypeScript.TypeScriptHookModel;

/// <summary>
/// Generates per-segment SWR hook files from OpenAPI operations. Built on the same operation model as
/// the React Query hooks (<see cref="TypeScriptHookModel"/>), so both take the same parameters, share the
/// query-key factory and unwrap results the same way:
/// <list type="bullet">
///   <item><description>GET / QUERY become <c>useSWR</c> hooks (every path, query, header and cookie parameter plus the body);</description></item>
///   <item><description>other verbs become <c>useSWRMutation</c> hooks that revalidate the segment's queries on success;</description></item>
///   <item><description>paginated streams get a <c>useSWRInfinite</c> hook, every stream a <c>useXxxStream</c> hook.</description></item>
/// </list>
/// The ApiClient owns retry (<c>x-retry-*</c>), so the hooks default to <c>shouldRetryOnError: false</c>.
/// </summary>
[SuppressMessage("Design", "MA0051:Method is too long", Justification = "Code generation methods require sequential StringBuilder operations.")]
public static class TypeScriptSwrHookExtractor
{
    public static List<(string FileName, string Content)> Extract(
        OpenApiDocument openApiDoc,
        string? headerContent,
        HashSet<string>? enumNames = null,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase,
        bool brandedIds = false,
        bool convertDates = false,
        HashSet<string>? writableSchemas = null)
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

            var content = GenerateSegmentHooks(segment, operations, openApiDoc, headerContent, enumNames, namingStrategy, convertDates, writableSchemas, brandedIds);
            if (content is not null)
            {
                results.Add(("use" + segment, content));
            }
        }

        return results;
    }

    private static string? GenerateSegmentHooks(
        string segment,
        List<(string Path, string Method, OpenApiOperation Operation)> operations,
        OpenApiDocument openApiDoc,
        string? headerContent,
        HashSet<string>? enumNames,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        HashSet<string> writableSchemas,
        bool brandedIds)
    {
        var importTypes = new HashSet<string>(StringComparer.Ordinal);
        var brandImports = new SortedSet<string>(StringComparer.Ordinal);
        var hookInfos = new List<HookInfo>();

        foreach (var (path, method, operation) in operations)
        {
            // A hook for the safe metadata verbs has no meaning.
            if (method.ToUpperInvariant() is "HEAD" or "OPTIONS" or "TRACE")
            {
                continue;
            }

            var info = ClassifyOperation(path, method, operation, openApiDoc, namingStrategy, writableSchemas);
            hookInfos.Add(info);

            if (brandedIds)
            {
                foreach (var param in info.PathParams)
                {
                    if (!string.IsNullOrEmpty(param.Name) &&
                        TypeScriptBrandedIdExtractor.ResolveParamBrand(info.Path, param.Name, param.Schema) is { } brand)
                    {
                        brandImports.Add(brand);
                    }
                }
            }

            TypeScriptOperationHelper.CollectImportTypes(operation, importTypes, openApiDoc, path);

            // A body that's a $ref to a schema with the readOnly/writeOnly split uses <Name>Writable.
            var (bodySchema, _) = operation.GetRequestBodySchemaWithContentType();
            if (bodySchema is OpenApiSchemaReference bodyRef &&
                (bodyRef.Reference.Id ?? bodyRef.Id) is { } refName &&
                writableSchemas.Contains(refName))
            {
                importTypes.Add(refName + TypeScriptModelExtractor.WritableSuffix);
            }
        }

        if (hookInfos.Count == 0)
        {
            return null;
        }

        var segmentCamel = segment.ToCamelCase();
        var body = new StringBuilder();
        AppendQueryKeyFactory(body, segmentCamel, hookInfos, namingStrategy, convertDates, brandedIds);

        foreach (var info in hookInfos)
        {
            body.AppendLine();
            if (info.IsStreaming)
            {
                AppendStreamHook(body, info, segmentCamel, namingStrategy, convertDates, brandedIds);
                if (info.IsPaginatedStreaming)
                {
                    body.AppendLine();
                    AppendInfiniteHook(body, info, segmentCamel, namingStrategy, convertDates, brandedIds);
                }
            }
            else if (info.IsQuery)
            {
                AppendQueryHook(body, info, segmentCamel, namingStrategy, convertDates, brandedIds);
            }
            else
            {
                AppendMutationHook(body, info, segmentCamel, namingStrategy, convertDates, brandedIds);
            }
        }

        var bodyText = body.ToString();
        var sb = new StringBuilder();
        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        AppendImports(sb, hookInfos, importTypes, enumNames, brandImports, bodyText);
        sb.Append(bodyText);
        return sb.ToString();
    }

    private static void AppendImports(
        StringBuilder sb,
        List<HookInfo> hookInfos,
        HashSet<string> importTypes,
        HashSet<string>? enumNames,
        SortedSet<string> brandImports,
        string bodyText)
    {
        var hasQueries = hookInfos.Any(h => h.IsQuery && !h.IsStreaming);
        var hasMutations = hookInfos.Any(h => !h.IsQuery && !h.IsStreaming);
        var hasStreams = hookInfos.Any(h => h.IsStreaming);
        var hasInfinite = hookInfos.Any(h => h.IsPaginatedStreaming);

        if (hasStreams)
        {
            sb.AppendLine("import { useCallback, useEffect, useRef, useState } from 'react';");
        }

        var swrImports = new List<string>();
        if (hasMutations)
        {
            swrImports.Add("useSWRConfig");
        }

        if (hasQueries)
        {
            swrImports.Add("type SWRConfiguration");
        }

        if (hasQueries)
        {
            sb.Append("import useSWR, { ").Append(string.Join(", ", swrImports)).AppendLine(" } from 'swr';");
        }
        else if (swrImports.Count > 0)
        {
            sb.Append("import { ").Append(string.Join(", ", swrImports)).AppendLine(" } from 'swr';");
        }

        if (hasInfinite)
        {
            sb.AppendLine("import useSWRInfinite, { type SWRInfiniteConfiguration } from 'swr/infinite';");
        }

        if (hasMutations)
        {
            sb.AppendLine("import useSWRMutation, { type SWRMutationConfiguration } from 'swr/mutation';");
        }

        sb.AppendLine("import { useApiService } from './useApiService';");

        if (hasQueries || hasMutations || hasInfinite)
        {
            sb.AppendLine("import { ApiError } from '../errors/ApiError';");
        }

        // Only the types the hooks actually name, so noUnusedLocals stays quiet.
        var modelImports = new SortedSet<string>(StringComparer.Ordinal);
        var enumImports = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var typeName in importTypes.Where(t => TypeScriptOperationHelper.ReferencesIdentifier(bodyText, t)))
        {
            if (enumNames?.Contains(typeName) == true)
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
    /// useSWR over the operation's client method. The key is the shared query key - or null (no request)
    /// while a path parameter or the body of a QUERY read is still missing, like React Query's enabled.
    /// </summary>
    private static void AppendQueryHook(
        StringBuilder sb,
        HookInfo info,
        string segmentCamel,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        var hookName = "use" + info.MethodName.ToPascalCaseForDotNet();
        var keyName = DeriveKeyName(info.MethodName, segmentCamel);
        var dataType = info.ReturnType == "void" ? "void" : info.ReturnType;

        var hookParams = new List<string>();
        var keyArgs = new List<string>();
        foreach (var param in info.PathParams)
        {
            var name = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            hookParams.Add(name + ": " + TypeScriptOperationHelper.GetParameterType(param, convertDates, brandedIds, info.Path));
            keyArgs.Add(name);
        }

        if (info.HasBody)
        {
            hookParams.Add("body: " + info.BodyType);
            keyArgs.Add("body");
        }

        foreach (var param in info.QuerystringParams)
        {
            var name = (param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            hookParams.Add(name + "?: string");
            keyArgs.Add(name);
        }

        if (info.QueryParams.Count > 0)
        {
            hookParams.Add("query?: " + TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy, convertDates));
            keyArgs.Add("query");
        }

        if (info.HeaderParams.Count > 0)
        {
            hookParams.Add("headers?: " + TypeScriptOperationHelper.BuildHeaderTypeInline(info.HeaderParams, convertDates));
        }

        if (info.CookieParams.Count > 0)
        {
            hookParams.Add("cookies?: " + BuildCookieTypeInline(info.CookieParams, namingStrategy));
        }

        hookParams.Add("config?: SWRConfiguration<" + dataType + ", ApiError>");

        var guards = new List<string>();
        if (info.PathParams.Count > 0)
        {
            guards.Add((info.PathParams[0].Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
        }

        if (info.HasBody)
        {
            guards.Add("body");
        }

        var key = segmentCamel + "Keys." + keyName + "(" + string.Join(", ", keyArgs) + ")";
        if (guards.Count > 0)
        {
            key = string.Join(" && ", guards) + " ? " + key + " : null";
        }

        var clientCallArgs = BuildClientCallArgs(info.PathParams, info.QueryParams, info.HeaderParams, info.CookieParams, info.QuerystringParams, hasBody: info.HasBody, namingStrategy: namingStrategy);

        AppendHookJsDoc(sb, info);
        sb.Append("export function ").Append(hookName).Append('(').Append(string.Join(", ", hookParams)).AppendLine(") {");
        sb.AppendLine("  const api = useApiService();");
        sb.Append("  return useSWR<").Append(dataType).AppendLine(", ApiError>(");
        sb.Append("    ").Append(key).AppendLine(",");
        sb.AppendLine("    async () => {");
        sb.Append("      const result = await api.").Append(segmentCamel).Append('.').Append(info.MethodName).Append('(').Append(clientCallArgs).AppendLine(");");
        AppendResultUnwrap(sb, info.ReturnType, info.HttpMethod, info.Success2xxDiscriminators);
        sb.AppendLine("    },");
        AppendNoRetryConfig(sb);
        sb.AppendLine("  );");
        sb.AppendLine("}");
    }

    /// <summary>
    /// useSWRMutation over the operation's client method; the trigger argument is the same variables
    /// object a React Query mutation takes. On success every query of the segment is revalidated.
    /// </summary>
    private static void AppendMutationHook(
        StringBuilder sb,
        HookInfo info,
        string segmentCamel,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
        var hookName = "use" + info.MethodName.ToPascalCaseForDotNet();
        var isVoidReturn = info.ReturnType == "void";
        var isDelete = info.HttpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase);
        var dataType = isVoidReturn || isDelete ? "void" : info.ReturnType;

        var signature = BuildMutationSignature(info, namingStrategy, convertDates, brandedIds);
        var argPattern = MutationArgPattern(signature.MutationArg);

        // No argument: `never` makes trigger() callable without one.
        var variablesType = argPattern is null ? "never" : signature.VariablesType;
        var keyType = "typeof " + segmentCamel + "Keys.all";

        var hookParams = new List<string>(signature.HookParams)
        {
            "config?: SWRMutationConfiguration<" + dataType + ", ApiError, " + keyType + ", " + variablesType + ">",
        };

        AppendHookJsDoc(sb, info);
        sb.Append("export function ").Append(hookName).Append('(').Append(string.Join(", ", hookParams)).AppendLine(") {");
        sb.AppendLine("  const api = useApiService();");
        sb.AppendLine("  const { mutate } = useSWRConfig();");
        sb.Append("  return useSWRMutation<").Append(dataType).Append(", ApiError, ").Append(keyType).Append(", ").Append(variablesType).AppendLine(">(");
        sb.Append("    ").Append(segmentCamel).AppendLine("Keys.all,");
        if (argPattern is null)
        {
            sb.AppendLine("    async () => {");
        }
        else
        {
            sb.AppendLine("    async (_key, { arg }) => {");
            sb.Append("      const ").Append(argPattern).AppendLine(" = arg;");
        }

        sb.Append("      const result = await api.").Append(segmentCamel).Append('.').Append(info.MethodName).Append('(').Append(signature.ClientCallArgs).AppendLine(");");
        AppendResultUnwrap(sb, info.ReturnType, info.HttpMethod, info.Success2xxDiscriminators);
        sb.AppendLine("    },");
        sb.AppendLine("    {");
        sb.AppendLine("      ...config,");
        sb.AppendLine("      onSuccess: (data, key, currentConfig) => {");
        sb.Append("        void mutate((cacheKey) => Array.isArray(cacheKey) && cacheKey[0] === '").Append(segmentCamel).AppendLine("');");
        sb.AppendLine("        config?.onSuccess?.(data, key, currentConfig);");
        sb.AppendLine("      },");
        sb.AppendLine("    },");
        sb.AppendLine("  );");
        sb.AppendLine("}");
    }

    /// <summary>
    /// useSWRInfinite over the non-streaming <c>&lt;methodName&gt;Page</c> companion of a paginated stream;
    /// each page passes the previous page's continuation token in the <c>x-continuation</c> header.
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

        var hookParams = new List<string>();
        var callArgs = new List<string>();
        foreach (var p in info.PathParams)
        {
            var name = (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            hookParams.Add(name + ": " + TypeScriptOperationHelper.GetParameterType(p, convertDates, brandedIds, info.Path));
            callArgs.Add(name);
        }

        var keyArgs = info.QueryParams.Count > 0 ? "query" : string.Empty;
        if (info.QueryParams.Count > 0)
        {
            hookParams.Add("query?: " + TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy, convertDates));
            callArgs.Add("query");
        }

        hookParams.Add("config?: SWRInfiniteConfiguration<" + info.PageReturnType + ", ApiError>");
        callArgs.Add("token ? { 'x-continuation': token } : undefined");

        AppendHookJsDoc(sb, info);
        sb.Append("export function ").Append(hookName).Append('(').Append(string.Join(", ", hookParams)).AppendLine(") {");
        sb.AppendLine("  const api = useApiService();");
        sb.Append("  return useSWRInfinite<").Append(info.PageReturnType).AppendLine(", ApiError>(");
        sb.AppendLine("    (_pageIndex, previousPage) => {");
        sb.AppendLine("      if (previousPage && !previousPage.continuationToken) {");
        sb.AppendLine("        return null;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.Append("      return [...").Append(segmentCamel).Append("Keys.").Append(infiniteKeyName).Append('(').Append(keyArgs).AppendLine("), previousPage?.continuationToken ?? null] as const;");
        sb.AppendLine("    },");
        sb.AppendLine("    async (key: readonly unknown[]) => {");
        sb.AppendLine("      const token = key[key.length - 1] as string | null;");
        sb.Append("      const result = await api.").Append(segmentCamel).Append('.').Append(pageMethodName).Append('(').Append(string.Join(", ", callArgs)).AppendLine(");");
        sb.AppendLine("      if (result.status === 'ok') {");
        sb.AppendLine("        return result.data;");
        sb.AppendLine("      }");
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
        AppendNoRetryConfig(sb);
        sb.AppendLine("  );");
        sb.AppendLine("}");
    }

    /// <summary>
    /// The ApiClient owns retry (x-retry-*); SWR's own error retry would multiply the attempts and re-send
    /// operations marked x-retry-enabled: false. The caller's config comes last and wins.
    /// </summary>
    private static void AppendNoRetryConfig(StringBuilder sb)
        => sb.AppendLine("    { shouldRetryOnError: false, ...config },");
}