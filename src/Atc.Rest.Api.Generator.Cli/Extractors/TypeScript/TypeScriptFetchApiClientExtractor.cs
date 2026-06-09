namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates the base ApiClient.ts with fetch wrapper, interfaces, and response handling.
/// </summary>
public static class TypeScriptFetchApiClientExtractor
{
    /// <summary>
    /// Generates the content for ApiClient.ts.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated file header.</param>
    /// <param name="convertDates">When true, emits dateReviver/dateReplacer for automatic Date conversion.</param>
    /// <returns>The TypeScript file content.</returns>
    public static string Generate(
        string? headerContent,
        bool convertDates = false,
        bool hasRetry = false,
        bool zodRuntimeValidate = false)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        AppendImports(sb, hasRetry, zodRuntimeValidate);

        if (convertDates)
        {
            AppendDateReviverReplacer(sb);
        }

        AppendApiClientOptionsInterface(sb, hasRetry);
        AppendRequestOptionsInterface(sb, zodRuntimeValidate);
        AppendApiClientClass(sb, convertDates, hasRetry, zodRuntimeValidate);

        return sb.ToString();
    }

    private static void AppendImports(
        StringBuilder sb,
        bool hasRetry,
        bool zodRuntimeValidate)
    {
        sb.AppendLine("import { ApiError } from '../errors/ApiError';");
        sb.AppendLine("import { ValidationError } from '../errors/ValidationError';");
        sb.AppendLine("import type { ApiResult } from '../types/ApiResult';");

        if (hasRetry)
        {
            sb.AppendLine("import { retryWithBackoff } from '../helpers/retryInterceptor';");
            sb.AppendLine("import { defaultRetryPolicy } from '../helpers/retryConfig';");
            sb.AppendLine("import type { RetryPolicy } from '../helpers/retryConfig';");
        }

        if (zodRuntimeValidate)
        {
            // ZodTypeAny is the broad parent type — accepts any z.object / z.array /
            // primitive / discriminated union. Type-only import keeps the bundle clean
            // for consumers who set the flag but never narrow into schemaMismatch.
            sb.AppendLine("import type { ZodTypeAny } from 'zod';");
        }

        sb.AppendLine();
    }

    private static void AppendApiClientOptionsInterface(
        StringBuilder sb,
        bool hasRetry)
    {
        sb.AppendLine("export type FetchRequestInterceptor = (url: string, init: RequestInit) => RequestInit | Promise<RequestInit>;");
        sb.AppendLine("export type FetchResponseInterceptor = (response: Response) => Response | Promise<Response>;");
        sb.AppendLine();
        sb.AppendLine("export interface ApiClientOptions {");
        sb.AppendLine("  getAccessToken?: () => string | Promise<string>;");
        sb.AppendLine("  defaultHeaders?: Record<string, string>;");
        sb.AppendLine("  requestInterceptors?: FetchRequestInterceptor[];");
        sb.AppendLine("  responseInterceptors?: FetchResponseInterceptor[];");

        if (hasRetry)
        {
            sb.AppendLine("  /** Retry policy for failed requests. Set to false to disable retry. Defaults to the spec-defined policy. */");
            sb.AppendLine("  retryPolicy?: RetryPolicy | false;");
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendRequestOptionsInterface(
        StringBuilder sb,
        bool zodRuntimeValidate)
    {
        sb.AppendLine("export interface RequestOptions {");
        sb.AppendLine("  body?: unknown;");
        sb.AppendLine("  query?: Record<string, string | number | boolean | (string | number | boolean)[] | undefined>;");
        sb.AppendLine("  headers?: Record<string, string | number | boolean | undefined>;");
        sb.AppendLine("  signal?: AbortSignal;");
        sb.AppendLine("  responseType?: 'json' | 'blob' | 'text';");
        if (zodRuntimeValidate)
        {
            // The schema is whatever the generated client method passes — z.object,
            // z.array, primitive, discriminated union. ApiClient only needs to call
            // .safeParse() on it; the broad ZodTypeAny captures every shape.
            sb.AppendLine("  parseSchema?: ZodTypeAny;");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Wire framing discriminator for streamed responses. Drives how requestStream parses the
        // body: 'json-array' | 'json-lines' | 'json-seq' use the brace-scan path; 'sse' and
        // 'multipart' each have a dedicated parse branch (Server-Sent Events `data: <json>\n\n`,
        // and boundary-delimited multipart/mixed parts respectively).
        sb.AppendLine("export type StreamFraming = 'json-array' | 'sse' | 'json-lines' | 'json-seq' | 'multipart';");
        sb.AppendLine();
    }

    private static void AppendDateReviverReplacer(StringBuilder sb)
    {
        sb.AppendLine("const ISO_DATE_RE = /^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}(:\\d{2})?(\\.\\d+)?(Z|[+-]\\d{2}:\\d{2})?$/;");
        sb.AppendLine();
        sb.AppendLine("function dateReviver(_key: string, value: unknown): unknown {");
        sb.AppendLine("  if (typeof value === 'string' && ISO_DATE_RE.test(value)) {");
        sb.AppendLine("    const parsed = new Date(value);");
        sb.AppendLine("    return Number.isNaN(parsed.getTime()) ? value : parsed;");
        sb.AppendLine("  }");
        sb.AppendLine("  return value;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("function dateReplacer(_key: string, value: unknown): unknown {");
        sb.AppendLine("  if (value instanceof Date) {");
        sb.AppendLine("    return value.toISOString();");
        sb.AppendLine("  }");
        sb.AppendLine("  return value;");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendApiClientClass(
        StringBuilder sb,
        bool convertDates,
        bool hasRetry,
        bool zodRuntimeValidate)
    {
        sb.AppendLine("export class ApiClient {");
        sb.AppendLine("  private readonly baseUrl: string;");
        sb.AppendLine("  private readonly options: ApiClientOptions;");

        if (hasRetry)
        {
            sb.AppendLine("  private readonly retryPolicy: RetryPolicy | false;");
        }

        if (zodRuntimeValidate)
        {
            // strictMode toggles between the "return schemaMismatch arm" (default, prod-
            // friendly) and "throw on validation failure" (opt-in, dev-friendly). Default
            // false matches the prod-shape "errors surface as values, not exceptions".
            sb.AppendLine("  private strictMode = false;");
        }

        sb.AppendLine();

        AppendConstructor(sb, hasRetry);

        if (zodRuntimeValidate)
        {
            AppendSetStrictModeMethod(sb);
        }

        AppendRequestMethod(sb, convertDates, hasRetry, zodRuntimeValidate);
        AppendRequestStreamMethod(sb, convertDates);
        AppendBuildUrlMethod(sb);
        AppendGetHeadersMethod(sb);
        AppendHandleResponseMethod(sb, convertDates, zodRuntimeValidate);

        sb.AppendLine("}");
    }

    /// <summary>
    /// Emits the <c>setStrictMode</c> toggle. When strict, schema-mismatch failures
    /// throw an Error with the Zod issues instead of surfacing the
    /// <c>schemaMismatch</c> arm — useful in dev/CI to fail fast on spec drift.
    /// </summary>
    private static void AppendSetStrictModeMethod(StringBuilder sb)
    {
        sb.AppendLine("  /**");
        sb.AppendLine("   * Toggle strict runtime validation. When enabled, schema-mismatch");
        sb.AppendLine("   * failures throw an Error with the Zod issues instead of returning a");
        sb.AppendLine("   * 'schemaMismatch' ApiResult arm. Useful in dev/staging to surface");
        sb.AppendLine("   * spec drift loudly; leave off in prod to keep errors as values.");
        sb.AppendLine("   */");
        sb.AppendLine("  setStrictMode(enabled: boolean): void {");
        sb.AppendLine("    this.strictMode = enabled;");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendConstructor(
        StringBuilder sb,
        bool hasRetry)
    {
        sb.AppendLine("  constructor(baseUrl: string, options?: ApiClientOptions) {");
        sb.AppendLine("    this.baseUrl = baseUrl.replace(/\\/+$/, '');");
        sb.AppendLine("    this.options = options ?? {};");

        if (hasRetry)
        {
            sb.AppendLine("    this.retryPolicy = this.options.retryPolicy !== undefined ? this.options.retryPolicy : defaultRetryPolicy;");
        }

        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendRequestMethod(
        StringBuilder sb,
        bool convertDates,
        bool hasRetry,
        bool zodRuntimeValidate)
    {
        var stringify = convertDates ? "JSON.stringify(options.body, dateReplacer)" : "JSON.stringify(options.body)";

        sb.AppendLine("  async request<T>(method: string, path: string, options?: RequestOptions): Promise<ApiResult<T>> {");
        sb.AppendLine("    const url = this.buildUrl(path, options?.query);");
        sb.AppendLine("    const headers = await this.getHeaders(options?.headers);");
        sb.AppendLine();
        sb.AppendLine("    let fetchBody: BodyInit | undefined;");
        sb.AppendLine("    if (options?.body !== undefined) {");
        sb.AppendLine("      if (options.body instanceof FormData) {");
        sb.AppendLine("        fetchBody = options.body;");
        sb.AppendLine("      } else if (options.body instanceof Blob) {");
        sb.AppendLine("        headers.set('Content-Type', 'application/octet-stream');");
        sb.AppendLine("        fetchBody = options.body;");
        sb.AppendLine("      } else {");
        sb.AppendLine("        headers.set('Content-Type', 'application/json');");
        sb.Append("        fetchBody = ").Append(stringify).AppendLine(";");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    let init: RequestInit = {");
        sb.AppendLine("      method,");
        sb.AppendLine("      headers,");
        sb.AppendLine("      body: fetchBody,");
        sb.AppendLine("      signal: options?.signal,");
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    for (const interceptor of this.options.requestInterceptors ?? []) {");
        sb.AppendLine("      init = await interceptor(url, init);");
        sb.AppendLine("    }");
        sb.AppendLine();

        if (hasRetry)
        {
            sb.AppendLine("    let response: Response;");
            sb.AppendLine("    if (this.retryPolicy) {");
            sb.AppendLine("      // Wire the per-attempt signal through so policy.timeoutMs actually cancels the");
            sb.AppendLine("      // in-flight fetch and parent aborts propagate to every retry attempt.");
            sb.AppendLine("      response = await retryWithBackoff(");
            sb.AppendLine("        (attemptSignal) => fetch(url, { ...init, signal: attemptSignal }),");
            sb.AppendLine("        this.retryPolicy,");
            sb.AppendLine("        options?.signal,");
            sb.AppendLine("      );");
            sb.AppendLine("    } else {");
            sb.AppendLine("      response = await fetch(url, init);");
            sb.AppendLine("    }");
        }
        else
        {
            sb.AppendLine("    let response = await fetch(url, init);");
        }

        sb.AppendLine();
        sb.AppendLine("    for (const interceptor of this.options.responseInterceptors ?? []) {");
        sb.AppendLine("      response = await interceptor(response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        if (zodRuntimeValidate)
        {
            sb.AppendLine("    return this.handleResponse<T>(response, options?.responseType, options?.parseSchema);");
        }
        else
        {
            sb.AppendLine("    return this.handleResponse<T>(response, options?.responseType);");
        }

        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendRequestStreamMethod(
        StringBuilder sb,
        bool convertDates)
    {
        var jsonParse = convertDates
            ? "JSON.parse(buffer.substring(objStart, objEnd + 1), dateReviver)"
            : "JSON.parse(buffer.substring(objStart, objEnd + 1))";

        // multipart/mixed parses each part body with the same date-revival posture as the
        // brace-scan path (parity — must not be strictly weaker than the sibling framings).
        var multipartParse = convertDates
            ? "JSON.parse(bodyText, dateReviver)"
            : "JSON.parse(bodyText)";

        sb.AppendLine("  async *requestStream<T>(method: string, path: string, options?: RequestOptions, framing: StreamFraming = 'json-array'): AsyncGenerator<T> {");
        sb.AppendLine("    const url = this.buildUrl(path, options?.query);");
        sb.AppendLine("    const headers = await this.getHeaders(options?.headers);");
        sb.AppendLine();
        sb.AppendLine("    let init: RequestInit = {");
        sb.AppendLine("      method,");
        sb.AppendLine("      headers,");
        sb.AppendLine("      signal: options?.signal,");
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    for (const interceptor of this.options.requestInterceptors ?? []) {");
        sb.AppendLine("      init = await interceptor(url, init);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const response = await fetch(url, init);");
        sb.AppendLine();
        sb.AppendLine("    if (!response.ok) {");
        sb.AppendLine("      const result = await this.handleResponse<T>(response);");
        sb.AppendLine("      if ('error' in result) {");
        sb.AppendLine("        throw result.error;");
        sb.AppendLine("      }");
        sb.AppendLine("      throw new ApiError(response.status, response.statusText, 'Stream request failed', response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const reader = response.body?.getReader();");
        sb.AppendLine("    if (!reader) {");
        sb.AppendLine("      throw new ApiError(0, 'NoBody', 'Response body is empty', response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const decoder = new TextDecoder();");
        sb.AppendLine("    let buffer = '';");
        sb.AppendLine();
        sb.AppendLine("    if (framing === 'sse') {");
        sb.AppendLine("      try {");
        sb.AppendLine("        while (true) {");
        sb.AppendLine("          const { done, value } = await reader.read();");
        sb.AppendLine("          if (done) break;");
        sb.AppendLine("          buffer += decoder.decode(value, { stream: true });");
        sb.AppendLine("          let sep: number;");
        sb.AppendLine("          while ((sep = buffer.indexOf('\\n\\n')) !== -1) {");
        sb.AppendLine("            const rawEvent = buffer.substring(0, sep);");
        sb.AppendLine("            buffer = buffer.substring(sep + 2);");
        sb.AppendLine("            const data = rawEvent");
        sb.AppendLine("              .split('\\n')");
        sb.AppendLine("              .filter((l) => l.startsWith('data:'))");
        sb.AppendLine("              .map((l) => l.slice(5).trimStart())");
        sb.AppendLine("              .join('\\n');");
        sb.AppendLine("            if (data.length > 0) {");
        sb.AppendLine("              yield JSON.parse(data) as T;");
        sb.AppendLine("            }");
        sb.AppendLine("          }");
        sb.AppendLine("        }");
        sb.AppendLine("      } finally {");
        sb.AppendLine("        reader.releaseLock();");
        sb.AppendLine("      }");
        sb.AppendLine("      return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (framing === 'multipart') {");
        sb.AppendLine("      // Buffer the whole body then split on the boundary: scanning a boundary token");
        sb.AppendLine("      // across streamed chunk edges is non-trivial, so this is an intentional");
        sb.AppendLine("      // simplification for multipart (the other framings parse incrementally).");
        sb.AppendLine("      const contentType = response.headers.get('content-type') ?? '';");
        sb.AppendLine("      const m = /boundary=(\"?)([^\";]+)\\1/i.exec(contentType);");
        sb.AppendLine("      const delimiter = '--' + (m ? m[2] : '');");
        sb.AppendLine("      let all = '';");
        sb.AppendLine("      try {");
        sb.AppendLine("        while (true) {");
        sb.AppendLine("          const { done, value } = await reader.read();");
        sb.AppendLine("          if (done) break;");
        sb.AppendLine("          all += decoder.decode(value, { stream: true });");
        sb.AppendLine("        }");
        sb.AppendLine("      } finally {");
        sb.AppendLine("        reader.releaseLock();");
        sb.AppendLine("      }");
        sb.AppendLine("      for (const part of all.split(delimiter)) {");
        sb.AppendLine("        const trimmed = part.replace(/^\\r\\n/, '');");
        sb.AppendLine("        if (trimmed.length === 0 || trimmed.startsWith('--')) continue;");
        sb.AppendLine("        const sep = trimmed.indexOf('\\r\\n\\r\\n');");
        sb.AppendLine("        if (sep === -1) continue;");
        sb.AppendLine("        const bodyText = trimmed.substring(sep + 4).trim();");
        sb.Append("        if (bodyText.length > 0) yield ").Append(multipartParse).AppendLine(" as T;");
        sb.AppendLine("      }");
        sb.AppendLine("      return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    try {");
        sb.AppendLine("      while (true) {");
        sb.AppendLine("        const { done, value } = await reader.read();");
        sb.AppendLine("        if (done) break;");
        sb.AppendLine();
        sb.AppendLine("        buffer += decoder.decode(value, { stream: true });");
        sb.AppendLine();
        sb.AppendLine("        // Extract complete JSON objects from buffer.");
        sb.AppendLine("        // Handles JSON array ([{...},{...}]) and NDJSON ({...}\\n{...}).");
        sb.AppendLine("        while (true) {");
        sb.AppendLine("          const objStart = buffer.indexOf('{');");
        sb.AppendLine("          if (objStart === -1) { buffer = ''; break; }");
        sb.AppendLine();
        sb.AppendLine("          let depth = 0, inStr = false, esc = false, objEnd = -1;");
        sb.AppendLine("          for (let i = objStart; i < buffer.length; i++) {");
        sb.AppendLine("            const ch = buffer[i];");
        sb.AppendLine("            if (esc) { esc = false; continue; }");
        sb.AppendLine("            if (ch === '\\\\' && inStr) { esc = true; continue; }");
        sb.AppendLine("            if (ch === '\"') { inStr = !inStr; continue; }");
        sb.AppendLine("            if (inStr) continue;");
        sb.AppendLine("            if (ch === '{') depth++;");
        sb.AppendLine("            if (ch === '}') { depth--; if (depth === 0) { objEnd = i; break; } }");
        sb.AppendLine("          }");
        sb.AppendLine();
        sb.AppendLine("          if (objEnd === -1) {");
        sb.AppendLine("            buffer = buffer.substring(objStart);");
        sb.AppendLine("            break;");
        sb.AppendLine("          }");
        sb.AppendLine();
        sb.Append("          yield ").Append(jsonParse).AppendLine(" as T;");
        sb.AppendLine("          buffer = buffer.substring(objEnd + 1);");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    } finally {");
        sb.AppendLine("      reader.releaseLock();");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendBuildUrlMethod(StringBuilder sb)
    {
        sb.AppendLine("  buildUrl(path: string, query?: Record<string, string | number | boolean | (string | number | boolean)[] | undefined>): string {");
        sb.AppendLine("    const url = new URL(`${this.baseUrl}${path}`);");
        sb.AppendLine("    if (query) {");
        sb.AppendLine("      for (const [key, value] of Object.entries(query)) {");
        sb.AppendLine("        if (value === undefined) {");
        sb.AppendLine("          continue;");
        sb.AppendLine("        }");
        sb.AppendLine("        if (Array.isArray(value)) {");
        sb.AppendLine("          for (const item of value) {");
        sb.AppendLine("            url.searchParams.append(key, String(item));");
        sb.AppendLine("          }");
        sb.AppendLine("        } else {");
        sb.AppendLine("          url.searchParams.set(key, String(value));");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("    // Note: URL.searchParams always percent-encodes values.");
        sb.AppendLine("    // OpenAPI allowReserved cannot be honoured via this path.");
        sb.AppendLine("    return url.toString();");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendGetHeadersMethod(StringBuilder sb)
    {
        // `extra` values may be undefined (the generated client passes through optional
        // header params verbatim), so skip those entries — Headers.set rejects undefined.
        // Non-string scalars (number / boolean) are coerced to string to match how
        // headers travel on the wire.
        sb.AppendLine("  async getHeaders(extra?: Record<string, string | number | boolean | undefined>): Promise<Headers> {");
        sb.AppendLine("    const headers = new Headers(this.options.defaultHeaders);");
        sb.AppendLine();
        sb.AppendLine("    if (this.options.getAccessToken) {");
        sb.AppendLine("      const token = await this.options.getAccessToken();");
        sb.AppendLine("      headers.set('Authorization', `Bearer ${token}`);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (extra) {");
        sb.AppendLine("      for (const [key, value] of Object.entries(extra)) {");
        sb.AppendLine("        if (value !== undefined) {");
        sb.AppendLine("          headers.set(key, String(value));");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    return headers;");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendHandleResponseMethod(
        StringBuilder sb,
        bool convertDates,
        bool zodRuntimeValidate)
    {
        var parseJson = convertDates
            ? "JSON.parse(await response.text(), dateReviver)"
            : "await response.json()";

        var signature = zodRuntimeValidate
            ? "  private async handleResponse<T>(response: Response, responseType?: 'json' | 'blob' | 'text', parseSchema?: ZodTypeAny): Promise<ApiResult<T>> {"
            : "  private async handleResponse<T>(response: Response, responseType?: 'json' | 'blob' | 'text'): Promise<ApiResult<T>> {";
        sb.AppendLine(signature);
        sb.AppendLine("    if (response.status === 204) {");
        sb.AppendLine("      return { status: 'noContent', response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const contentType = response.headers.get('Content-Type') ?? '';");
        sb.AppendLine("    const isText = responseType === 'text' || (!responseType && (contentType.startsWith('text/') || contentType.includes('application/xml')));");
        sb.AppendLine("    const isJson = responseType ? responseType === 'json' : (!isText && contentType.includes('application/json'));");
        sb.AppendLine();
        sb.AppendLine("    if (response.ok) {");
        sb.AppendLine("      let data: unknown;");
        sb.AppendLine("      try {");
        sb.Append("        data = isText ? await response.text() : isJson ? ").Append(parseJson).AppendLine(" : await response.blob();");
        sb.AppendLine("      } catch (parseError) {");
        sb.AppendLine("        return { status: 'parseError', error: parseError as Error, response };");
        sb.AppendLine("      }");

        if (zodRuntimeValidate)
        {
            // Validate JSON payloads only — text/blob responses aren't structured, so the
            // schema can't speak to them. The `safeParse` path returns the issues without
            // throwing; strictMode escalates to an exception so dev/CI can fail fast.
            sb.AppendLine();
            sb.AppendLine("      if (parseSchema && isJson) {");
            sb.AppendLine("        const parsed = parseSchema.safeParse(data);");
            sb.AppendLine("        if (!parsed.success) {");
            sb.AppendLine("          if (this.strictMode) {");
            sb.AppendLine("            const issuesSummary = parsed.error.issues.map(i => `${i.path.join('.')}: ${i.message}`).join('; ');");
            sb.AppendLine("            throw new Error(`Schema mismatch: ${issuesSummary}`);");
            sb.AppendLine("          }");
            sb.AppendLine();
            sb.AppendLine("          return { status: 'schemaMismatch', issues: parsed.error.issues, data, response };");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        data = parsed.data;");
            sb.AppendLine("      }");
            sb.AppendLine();
        }

        sb.AppendLine("      const status = response.status === 201");
        sb.AppendLine("        ? 'created' as const");
        sb.AppendLine("        : response.status === 202");
        sb.AppendLine("          ? 'accepted' as const");
        sb.AppendLine("          : 'ok' as const;");
        sb.AppendLine("      return { status, data: data as T, response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // Malformed JSON in an error response is non-fatal — fall back to statusText.");
        sb.AppendLine("    let errorBody: { title?: string; message?: string; errors?: Record<string, string[]> } | null = null;");
        sb.AppendLine("    if (isJson) {");
        sb.AppendLine("      try {");
        sb.Append("        errorBody = ").Append(parseJson).AppendLine(";");
        sb.AppendLine("      } catch {");
        sb.AppendLine("        errorBody = null;");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("    const message = errorBody?.title ?? errorBody?.message ?? response.statusText;");
        sb.AppendLine();
        sb.AppendLine("    if (response.status === 400 && errorBody?.errors) {");
        sb.AppendLine("      return {");
        sb.AppendLine("        status: 'badRequest',");
        sb.AppendLine("        error: new ValidationError(response.status, response.statusText, message, errorBody.errors, response),");
        sb.AppendLine("        response,");
        sb.AppendLine("      };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const apiError = new ApiError(response.status, response.statusText, message, response);");
        sb.AppendLine();
        sb.AppendLine("    switch (response.status) {");
        sb.AppendLine("      case 401:");
        sb.AppendLine("        return { status: 'unauthorized', error: apiError, response };");
        sb.AppendLine("      case 403:");
        sb.AppendLine("        return { status: 'forbidden', error: apiError, response };");
        sb.AppendLine("      case 404:");
        sb.AppendLine("        return { status: 'notFound', error: apiError, response };");
        sb.AppendLine("      case 409:");
        sb.AppendLine("        return { status: 'conflict', error: apiError, response };");
        sb.AppendLine("      case 422:");
        sb.AppendLine("        return { status: 'unprocessableEntity', error: apiError, response };");
        sb.AppendLine("      case 429:");
        sb.AppendLine("        return { status: 'tooManyRequests', error: apiError, response };");
        sb.AppendLine("      default:");
        sb.AppendLine("        return { status: 'serverError', error: apiError, response };");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
    }
}