// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable InvertIf
namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// The framework-agnostic part of the generated TypeScript hooks, shared by the React Query and SWR
/// hook extractors so both describe an operation the same way: its classification (query, mutation,
/// stream), parameters, query-key factory, client call, result unwrap, JSDoc and the stream hook.
/// </summary>
[SuppressMessage("Design", "MA0051:Method is too long", Justification = "Code generation methods require sequential StringBuilder operations.")]
internal static class TypeScriptHookModel
{
    internal static void AppendQueryKeyFactory(
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

            // Build key function param list and key spread values incrementally.
            // Order: pathParams..., body, querystringParams (individual)..., query?
            // Headers and cookies are intentionally excluded — they fragment the cache
            // per-request rather than per-resource, which breaks cache sharing.
            var keyFuncParts = new List<string>();
            var keySpreadValues = new List<string>();

            foreach (var p in info.PathParams)
            {
                var pName = (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
                keyFuncParts.Add(pName + ": " + TypeScriptOperationHelper.GetParameterType(p, convertDates, brandedIds, info.Path));
                keySpreadValues.Add(pName);
            }

            // A read that carries a body (OpenAPI 3.2 QUERY) is identified by that body, so it
            // has to participate in the cache key — otherwise every distinct criteria set would
            // collide on one entry. React Query hashes the key structurally, so the body object
            // is used directly rather than a digest: a digest would cost the same serialization
            // and lose readability in the devtools.
            if (info.HasBody)
            {
                keyFuncParts.Add("body: " + info.BodyType);
                keySpreadValues.Add("body");
            }

            foreach (var p in info.QuerystringParams)
            {
                var pName = (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
                keyFuncParts.Add(pName + "?: string");
                keySpreadValues.Add(pName);
            }

            if (info.QueryParams.Count > 0)
            {
                var queryType = TypeScriptOperationHelper.BuildQueryTypeInline(info.QueryParams, namingStrategy, convertDates);
                keyFuncParts.Add("query?: " + queryType);
                keySpreadValues.Add("query");
            }

            if (keyFuncParts.Count > 0)
            {
                var spreadSuffix = keySpreadValues.Count > 0
                    ? ", " + string.Join(", ", keySpreadValues)
                    : string.Empty;
                sb.Append("  ").Append(keyName).Append(": (").Append(string.Join(", ", keyFuncParts)).Append(") => [...").Append(segmentCamel).Append("Keys.all, '").Append(keyName).Append('\'').Append(spreadSuffix).AppendLine("] as const,");
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

    /// <summary>
    /// Emits Option A from issues/001-feedback.md: a useState/useEffect/useRef-backed hook
    /// that consumes the AsyncGenerator-style client method and accumulates items into local
    /// state. Aborts on unmount or param change, swallows AbortError, exposes a 4-state
    /// status machine plus cancel/reset controls.
    /// </summary>
    internal static void AppendStreamHook(
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

    internal static void AppendResultUnwrap(
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
                : ["noContent", "ok"];
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
    }

    internal static string BuildStatusDisjunction(List<string> discriminators)
        => string.Join(
            " || ",
            discriminators.Select(d => $"result.status === '{d}'"));

    internal static string BuildClientCallArgs(
        List<OpenApiParameter> pathParams,
        List<OpenApiParameter> queryParams,
        List<OpenApiParameter> headerParams,
        List<OpenApiParameter> cookieParams,
        List<OpenApiParameter> querystringParams,
        bool hasBody,
        TypeScriptNamingStrategy namingStrategy = TypeScriptNamingStrategy.CamelCase)
    {
        // Argument order must mirror the client method's parameter list:
        //   pathParams..., body, querystringParams (individual)..., query, headers, cookies
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

        foreach (var param in querystringParams)
        {
            args.Add((param.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy));
        }

        if (queryParams.Count > 0)
        {
            args.Add("query");
        }

        if (headerParams.Count > 0)
        {
            args.Add("headers");
        }

        if (cookieParams.Count > 0)
        {
            args.Add("cookies");
        }

        return string.Join(", ", args);
    }

    internal static string BuildCookieTypeInline(
        List<OpenApiParameter> cookieParams,
        TypeScriptNamingStrategy namingStrategy)
    {
        var props = cookieParams.Select(p =>
        {
            var name = (p.Name ?? string.Empty).ApplyNamingStrategy(namingStrategy);
            var optional = p.Required ? string.Empty : "?";
            return name + optional + ": string";
        });
        return "{ " + string.Join("; ", props) + " }";
    }

    /// <summary>
    /// Derives a query key entry name from an operationId.
    /// Strips the segment prefix and converts remainder to camelCase.
    /// Examples: listAccounts -> list, getAccountById -> detail, listPaginatedAccounts -> listPaginated.
    /// </summary>
    internal static string DeriveKeyName(
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

    internal static HookInfo ClassifyOperation(
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
        var cookieParams = TypeScriptOperationHelper.GetMergedParameters(
            operation, openApiDoc, path, ParameterLocation.Cookie);
        var querystringParams = TypeScriptOperationHelper.GetMergedParameters(
            operation, openApiDoc, path, ParameterLocation.QueryString);

        var returnType = TypeScriptOperationHelper.GetReturnType(operation, isStreaming, isFileDownload);

        var (bodySchema, bodyContentType) = operation.GetRequestBodySchemaWithContentType();
        var hasBody = bodySchema is not null && !isFileUpload;
        var bodyType = hasBody ? bodySchema!.ToTypeScriptReturnType() : string.Empty;

        // Mirror the client extractor: a body that's a $ref to a schema with the
        // readOnly/writeOnly split must use the <Name>Writable variant in the mutation
        // signature so consumers can't accidentally pass readOnly fields.
        if (hasBody && bodySchema is OpenApiSchemaReference bodyRef)
        {
            var bodyRefName = bodyRef.Reference.Id ?? bodyRef.Id;
            if (bodyRefName is not null && writableSchemas.Contains(bodyRefName))
            {
                bodyType = bodyRefName + TypeScriptModelExtractor.WritableSuffix;
            }
        }

        // Determine file upload parameter info
        var hasFileUploadArg = false;
        var fileUploadParam = string.Empty;
        var fileUploadArgName = string.Empty;
        if (isFileUpload && bodySchema is not null)
        {
            (hasFileUploadArg, fileUploadParam, fileUploadArgName) = GetFileUploadInfo(bodySchema, bodyContentType, namingStrategy);
        }

        // GET / QUERY + not file download + not streaming => useQuery
        // GET + file download => useMutation (user-triggered)
        // Streaming => skip
        // Everything else => useMutation
        //
        // The OpenAPI 3.2 QUERY method is safe and idempotent — a read whose criteria travel
        // in the request body because they are too large for a URL. It belongs in useQuery
        // (cached, refetchable) exactly like GET, not in useMutation.
        var isQuery = (httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
                       httpMethod.Equals("QUERY", StringComparison.OrdinalIgnoreCase))
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
            CookieParams: cookieParams,
            QuerystringParams: querystringParams,
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
    internal static void AppendHookJsDoc(
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

    internal static (bool HasArg, string ParamDecl, string ArgName) GetFileUploadInfo(
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
            if (bodySchema is OpenApiSchema actualSchema && actualSchema.Required is not null)
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

    internal sealed record HookInfo(
        string MethodName,
        string HttpMethod,
        bool IsQuery,
        bool IsStreaming,
        List<OpenApiParameter> PathParams,
        List<OpenApiParameter> QueryParams,
        List<OpenApiParameter> HeaderParams,
        List<OpenApiParameter> CookieParams,
        List<OpenApiParameter> QuerystringParams,
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

    /// <summary>
    /// The shape of a mutation hook: the hook-scoped parameters (path params beside a body, query,
    /// headers), the mutation argument (<c>(body: Pet)</c>, <c>(petId: string)</c>, <c>()</c>), its
    /// variables type, and the client call arguments. Shared by the React Query and SWR hooks.
    /// </summary>
    internal static MutationSignature BuildMutationSignature(
        HookInfo info,
        TypeScriptNamingStrategy namingStrategy,
        bool convertDates,
        bool brandedIds)
    {
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
            clientCallArgs = BuildClientCallArgs(info.PathParams, info.QueryParams, info.HeaderParams, info.CookieParams, info.QuerystringParams, hasBody: true, namingStrategy: namingStrategy);
            variablesType = info.BodyType;
        }
        else if (info.HasBody)
        {
            // Body only as mutation arg
            mutationArg = "(body: " + info.BodyType + ")";
            clientCallArgs = BuildClientCallArgs(info.PathParams, info.QueryParams, info.HeaderParams, info.CookieParams, info.QuerystringParams, hasBody: true, namingStrategy: namingStrategy);
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

        return new MutationSignature(hookParams, mutationArg, clientCallArgs, variablesType);
    }

    /// <summary>
    /// The binding pattern of a mutation argument - <c>body</c> for <c>(body: Pet)</c>,
    /// <c>{ petId, ...data }</c> for a destructured one - or null for <c>()</c>.
    /// </summary>
    internal static string? MutationArgPattern(string mutationArg)
    {
        var inner = mutationArg.Length >= 2 ? mutationArg[1..^1].Trim() : string.Empty;
        if (inner.Length == 0)
        {
            return null;
        }

        if (inner.StartsWith('{'))
        {
            var close = inner.IndexOf('}', StringComparison.Ordinal);
            return inner[..(close + 1)];
        }

        var colon = inner.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 ? inner[..colon].Trim() : inner;
    }

    /// <summary>
    /// The parts of a generated mutation hook, see <see cref="BuildMutationSignature"/>.
    /// </summary>
    internal sealed record MutationSignature(
        List<string> HookParams,
        string MutationArg,
        string ClientCallArgs,
        string VariablesType);
}