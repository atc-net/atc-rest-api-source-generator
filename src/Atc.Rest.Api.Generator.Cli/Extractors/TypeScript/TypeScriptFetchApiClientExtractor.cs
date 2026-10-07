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
    /// <param name="hasRetry">When true, the spec declares retry (x-retry-*): the client retries through helpers/retryInterceptor.ts.</param>
    /// <param name="zodRuntimeValidate">When true, responses are validated against their Zod schema.</param>
    /// <param name="hasWireNames">
    /// When true, the TypeScript models' property names differ from the API's for some keys: bodies, responses,
    /// error bodies and stream items are renamed through <c>helpers/wireNames.ts</c>.
    /// </param>
    /// <param name="convertDates">When true (<c>--convert-dates</c>), the file exports <c>dateOnly</c>, which the segment clients write a <c>format: date</c> parameter with.</param>
    /// <returns>The TypeScript file content.</returns>
    public static string Generate(
        string? headerContent,
        bool hasRetry = false,
        bool zodRuntimeValidate = false,
        bool hasWireNames = false,
        bool convertDates = false)
    {
        var sb = new StringBuilder();

        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        AppendImports(sb, hasRetry, zodRuntimeValidate, hasWireNames);
        if (convertDates)
        {
            TypeScriptOperationHelper.AppendDateOnlyHelper(sb);
        }

        AppendApiClientOptionsInterface(sb, hasRetry);
        AppendRequestOptionsInterface(sb, hasRetry, zodRuntimeValidate, hasWireNames);
        TypeScriptOperationHelper.AppendParseEventStreamHelper(sb);
        AppendApiClientClass(sb, hasRetry, zodRuntimeValidate, hasWireNames);

        return sb.ToString();
    }

    private static void AppendImports(
        StringBuilder sb,
        bool hasRetry,
        bool zodRuntimeValidate,
        bool hasWireNames)
    {
        sb.AppendLine("import { ApiError, errorMessage } from '../errors/ApiError';");
        sb.AppendLine("import { ValidationError } from '../errors/ValidationError';");
        if (hasWireNames)
        {
            sb.AppendLine("import { fromWire, toWire, type WireShapes } from '../helpers/wireNames';");
        }

        sb.AppendLine("import type { ApiResult } from '../types/ApiResult';");

        if (hasRetry)
        {
            sb.AppendLine("import { resolveRetryPolicy, retryWithBackoff } from '../helpers/retryInterceptor';");
            sb.AppendLine("import { defaultRetryPolicy } from '../helpers/retryConfig';");
            sb.AppendLine("import type { RequestRetry, RetryPolicy } from '../helpers/retryConfig';");
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
        bool hasRetry,
        bool zodRuntimeValidate,
        bool hasWireNames)
    {
        sb.AppendLine("export interface RequestOptions {");
        sb.AppendLine("  body?: unknown;");
        sb.AppendLine("  query?: Record<string, string | number | boolean | (string | number | boolean)[] | undefined>;");
        sb.AppendLine("  headers?: Record<string, string | number | boolean | undefined>;");
        sb.AppendLine("  signal?: AbortSignal;");
        sb.AppendLine("  responseType?: 'json' | 'blob' | 'text';");
        sb.AppendLine("  /** The media type of a file sent as the raw body (the generated client passes the declared one); else the file's own type. */");
        sb.AppendLine("  contentType?: string;");
        sb.AppendLine("  /** The result arms of the operation's union (the generated client passes them): a status it does not declare is folded into one it does. */");
        sb.AppendLine("  arms?: readonly string[];");
        if (hasRetry)
        {
            sb.AppendLine("  /** Retry for this request (the generated client passes it for operations with their own x-retry-* settings). */");
            sb.AppendLine("  retry?: RequestRetry;");
        }

        if (zodRuntimeValidate)
        {
            // The schema is whatever the generated client method passes — z.object,
            // z.array, primitive, discriminated union. ApiClient only needs to call
            // .safeParse() on it; the broad ZodTypeAny captures every shape.
            sb.AppendLine("  parseSchema?: ZodTypeAny;");
        }

        if (hasWireNames)
        {
            sb.AppendLine("  /** How to rename the keys of the body, the response and the error bodies between the API's property names and the models' (the generated client passes it). */");
            sb.AppendLine("  wire?: WireShapes;");
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

    private static void AppendApiClientClass(
        StringBuilder sb,
        bool hasRetry,
        bool zodRuntimeValidate,
        bool hasWireNames)
    {
        TypeScriptResultArms.AppendFitToArms(sb);
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

        AppendRequestMethod(sb, hasRetry, zodRuntimeValidate, hasWireNames);
        if (hasWireNames)
        {
            TypeScriptOperationHelper.AppendRenamingRequestStream(sb);
        }

        AppendRequestStreamMethod(sb, hasWireNames);
        AppendBuildUrlMethod(sb);
        AppendGetHeadersMethod(sb);
        AppendHandleResponseMethod(sb, zodRuntimeValidate, hasWireNames);

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
        bool hasRetry,
        bool zodRuntimeValidate,
        bool hasWireNames)
    {
        var body = hasWireNames ? "toWire(options.body, options.wire?.body)" : "options.body";
        var stringify = $"JSON.stringify({body})";

        sb.AppendLine("  async request<T>(method: string, path: string, options?: RequestOptions): Promise<ApiResult<T>> {");
        sb.AppendLine("    const url = this.buildUrl(path, options?.query);");
        sb.AppendLine("    const headers = await this.getHeaders(options?.headers);");
        sb.AppendLine();
        sb.AppendLine("    let fetchBody: BodyInit | undefined;");
        sb.AppendLine("    if (options?.body !== undefined) {");
        sb.AppendLine("      if (options.body instanceof FormData) {");
        sb.AppendLine("        fetchBody = options.body;");
        sb.AppendLine("      } else if (options.body instanceof URLSearchParams) {");
        sb.AppendLine("        headers.set('Content-Type', 'application/x-www-form-urlencoded');");
        sb.AppendLine("        fetchBody = options.body;");
        sb.AppendLine("      } else if (options.body instanceof Blob) {");
        sb.AppendLine("        headers.set('Content-Type', options.contentType ?? (options.body.type || 'application/octet-stream'));");
        sb.AppendLine("        fetchBody = options.body;");
        sb.AppendLine("      } else {");
        sb.AppendLine("        headers.set('Content-Type', options.contentType ?? 'application/json');");
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
            sb.AppendLine("    const retryPolicy = resolveRetryPolicy(method, this.retryPolicy, options?.retry);");
            sb.AppendLine("    if (retryPolicy) {");
            sb.AppendLine("      // Each attempt gets its own signal, so policy.timeoutMs cancels the in-flight fetch and a parent abort");
            sb.AppendLine("      // reaches every attempt; the body is read inside the attempt, so both cover the body read too.");
            sb.AppendLine("      return retryWithBackoff(");
            sb.AppendLine("        (attemptSignal) => fetch(url, { ...init, signal: attemptSignal }),");
            sb.AppendLine("        retryPolicy,");
            sb.AppendLine("        options?.signal,");
            sb.AppendLine("        (response) => this.readResponse<T>(response, options),");
            sb.AppendLine("      );");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("    return this.readResponse<T>(await fetch(url, init), options);");
        sb.AppendLine("  }");
        sb.AppendLine();

        sb.AppendLine("  private async readResponse<T>(response: Response, options?: RequestOptions): Promise<ApiResult<T>> {");
        sb.AppendLine("    for (const interceptor of this.options.responseInterceptors ?? []) {");
        sb.AppendLine("      response = await interceptor(response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        var wireArgument = hasWireNames ? ", options?.wire" : string.Empty;
        if (zodRuntimeValidate)
        {
            sb.Append("    return fitToArms(await this.handleResponse<T>(response, options?.responseType, options?.parseSchema").Append(wireArgument).AppendLine("), options?.arms);");
        }
        else
        {
            sb.Append("    return fitToArms(await this.handleResponse<T>(response, options?.responseType").Append(wireArgument).AppendLine("), options?.arms);");
        }

        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendRequestStreamMethod(
        StringBuilder sb,
        bool hasWireNames)
    {
        const string multipartParse = "JSON.parse(bodyText)";

        // With renaming, requestStream (above) renames each item this reader yields.
        sb.AppendLine(hasWireNames
            ? "  private async *readStream<T>(method: string, path: string, options?: RequestOptions, framing: StreamFraming = 'json-array'): AsyncGenerator<T> {"
            : "  async *requestStream<T>(method: string, path: string, options?: RequestOptions, framing: StreamFraming = 'json-array'): AsyncGenerator<T> {");
        sb.AppendLine("    const url = this.buildUrl(path, options?.query);");
        sb.AppendLine("    const headers = await this.getHeaders(options?.headers);");
        sb.AppendLine();
        TypeScriptOperationHelper.AppendStreamRequestBody(sb, hasWireNames);
        sb.AppendLine("    let init: RequestInit = {");
        sb.AppendLine("      method,");
        sb.AppendLine("      headers,");
        sb.AppendLine("      body: streamBody,");
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
        sb.AppendLine();
        sb.AppendLine("    if (framing === 'sse') {");
        sb.AppendLine("      yield* parseEventStream<T>(reader, decoder);");
        sb.AppendLine("      return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (framing === 'multipart') {");
        sb.AppendLine("      const contentType = response.headers.get('content-type') ?? '';");
        sb.AppendLine("      const m = /boundary=(\"?)([^\";]+)\\1/i.exec(contentType);");
        sb.AppendLine("      // Without a boundary the parts cannot be found: an error, not a guess");
        sb.AppendLine("      if (!m) throw new Error('A multipart/mixed response without a boundary in its Content-Type cannot be split into its parts.');");
        sb.AppendLine("      const delimiter = '--' + m[2];");
        sb.AppendLine("      let buf = '';");
        sb.AppendLine("      let finished = false;");
        sb.AppendLine("      try {");
        sb.AppendLine("        while (true) {");
        sb.AppendLine("          const { done, value } = await reader.read();");
        sb.AppendLine("          if (done) {");
        sb.AppendLine("            finished = true;");
        sb.AppendLine("            // A last part the stream ended on without its closing boundary (the server failed mid-stream): what it sent is yielded");
        sb.AppendLine("            if (buf.startsWith(delimiter)) {");
        sb.AppendLine("              const tail = buf.substring(delimiter.length).replace(/^\\r\\n/, '');");
        sb.AppendLine("              const sep = tail.indexOf('\\r\\n\\r\\n');");
        sb.AppendLine("              const bodyText = sep === -1 || tail.startsWith('--') ? '' : tail.substring(sep + 4).trim();");
        sb.AppendLine("              if (bodyText.length > 0 && scanJsonValue(bodyText, 0, true) === bodyText.length) yield JSON.parse(bodyText) as T;");
        sb.AppendLine("            }");
        sb.AppendLine("            break;");
        sb.AppendLine("          }");
        sb.AppendLine("          buf += decoder.decode(value, { stream: true });");
        sb.AppendLine("          // Discard preamble (everything before the first delimiter boundary).");
        sb.AppendLine("          const first = buf.indexOf(delimiter);");
        sb.AppendLine("          if (first === -1) { if (buf.length > delimiter.length) buf = buf.substring(buf.length - delimiter.length); continue; }");
        sb.AppendLine("          if (first > 0) buf = buf.substring(first);");
        sb.AppendLine("          // Yield each complete part; buf always starts at a delimiter after this loop.");
        sb.AppendLine("          while (buf.startsWith(delimiter)) {");
        sb.AppendLine("            const partStart = delimiter.length;");
        sb.AppendLine("            const next = buf.indexOf(delimiter, partStart);");
        sb.AppendLine("            if (next === -1) {");
        sb.AppendLine("              // A part whose body is complete JSON is yielded now, not at the next boundary - a server that fails after it never sends one");
        sb.AppendLine("              const pending = buf.substring(partStart).replace(/^\\r\\n/, '');");
        sb.AppendLine("              const pendingSep = pending.indexOf('\\r\\n\\r\\n');");
        sb.AppendLine("              const pendingBody = pendingSep === -1 || pending.startsWith('--') ? '' : pending.substring(pendingSep + 4).trim();");
        sb.AppendLine("              if (pendingBody.length > 0 && pending.endsWith('\\r\\n') && scanJsonValue(pendingBody, 0, true) === pendingBody.length) { yield JSON.parse(pendingBody) as T; buf = ''; }");
        sb.AppendLine("              break;");
        sb.AppendLine("            }");
        sb.AppendLine("            const part = buf.substring(partStart, next);");
        sb.AppendLine("            buf = buf.substring(next);");
        sb.AppendLine("            const trimmed = part.replace(/^\\r\\n/, '');");
        sb.AppendLine("            if (trimmed.length === 0 || trimmed.startsWith('--')) continue;");
        sb.AppendLine("            const sep = trimmed.indexOf('\\r\\n\\r\\n');");
        sb.AppendLine("            if (sep === -1) continue;");
        sb.AppendLine("            const bodyText = trimmed.substring(sep + 4).trim();");
        sb.Append("            if (bodyText.length > 0) yield ").Append(multipartParse).AppendLine(" as T;");
        sb.AppendLine("          }");
        sb.AppendLine("        }");
        sb.AppendLine("      } finally {");
        sb.AppendLine("        await releaseReader(reader, finished);");
        sb.AppendLine("      }");
        sb.AppendLine("      return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // JSON Lines, JSON Text Sequence, or a JSON array read one element at a time");
        sb.AppendLine("    if (framing === 'json-lines') {");
        sb.AppendLine("      yield* parseJsonLinesStream<T>(reader, decoder);");
        sb.AppendLine("    } else if (framing === 'json-seq') {");
        sb.AppendLine("      yield* parseJsonSeqStream<T>(reader, decoder);");
        sb.AppendLine("    } else {");
        sb.AppendLine("      yield* parseJsonArrayStream<T>(reader, decoder);");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendBuildUrlMethod(StringBuilder sb)
    {
        sb.AppendLine("  buildUrl(path: string, query?: Record<string, string | number | boolean | (string | number | boolean)[] | undefined>): string {");
        sb.AppendLine("    // A relative base URL ('/api', or an empty one) resolves against the origin of the page");
        sb.AppendLine("    const origin = (globalThis as { location?: { origin?: string } }).location?.origin;");
        sb.AppendLine("    const url = new URL(`${this.baseUrl}${path}`, origin);");
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
        bool zodRuntimeValidate,
        bool hasWireNames)
    {
        const string parseJson = "await response.json()";

        var wireParameter = hasWireNames ? ", wire?: WireShapes" : string.Empty;
        var signature = zodRuntimeValidate
            ? "  private async handleResponse<T>(response: Response, responseType?: 'json' | 'blob' | 'text', parseSchema?: ZodTypeAny" + wireParameter + "): Promise<ApiResult<T>> {"
            : "  private async handleResponse<T>(response: Response, responseType?: 'json' | 'blob' | 'text'" + wireParameter + "): Promise<ApiResult<T>> {";
        sb.AppendLine(signature);
        sb.AppendLine("    // 204 and 205 never carry a body; 304 neither (RFC 9110).");
        sb.AppendLine("    if (response.status === 204 || response.status === 205) {");
        sb.AppendLine("      return { status: 'noContent', response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (response.status === 304) {");
        sb.AppendLine("      return { status: 'notModified', response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const contentType = (response.headers.get('Content-Type') ?? '').toLowerCase();");
        sb.AppendLine("    const isText = responseType === 'text' || (!responseType && (contentType.startsWith('text/') || contentType.includes('application/xml') || contentType.includes('+xml')));");

        // Treat +json suffixes (application/problem+json, application/vnd.api+json) as JSON too,
        // so RFC-7807 error bodies parse and their title/errors reach the badRequest arm instead
        // of falling through to blob() and being lost.
        sb.AppendLine("    const isJson = responseType ? responseType === 'json' : (!isText && (contentType.includes('application/json') || contentType.includes('+json')));");
        sb.AppendLine();
        sb.AppendLine("    if (response.ok) {");
        sb.AppendLine("      // A JSON operation reads JSON whatever the Content-Type says; an empty body is no data. Without a");
        sb.AppendLine("      // responseType the Content-Type decides, and a body without one is its text (empty: no data).");
        sb.AppendLine("      let data: unknown;");
        sb.AppendLine("      try {");
        sb.AppendLine("        if (isText) {");
        sb.AppendLine("          data = await response.text();");
        sb.AppendLine("        } else if (isJson) {");
        sb.AppendLine("          const text = await response.text();");
        sb.AppendLine("          data = text.length === 0 ? undefined : JSON.parse(text);");
        sb.AppendLine("        } else if (responseType === 'blob' || contentType.length > 0) {");
        sb.AppendLine("          data = await response.blob();");
        sb.AppendLine("        } else {");
        sb.AppendLine("          const text = await response.text();");
        sb.AppendLine("          data = text.length === 0 ? undefined : text;");
        sb.AppendLine("        }");
        sb.AppendLine("      } catch (parseError) {");
        sb.AppendLine("        // Only a malformed body is a parseError; a body read that is aborted or cut off rejects like the request.");
        sb.AppendLine("        if (!(parseError instanceof SyntaxError)) {");
        sb.AppendLine("          throw parseError;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return { status: 'parseError', error: parseError, response };");
        sb.AppendLine("      }");

        if (hasWireNames)
        {
            // The API's property names -> the models' (before a schema check, which speaks the models' names).
            sb.AppendLine();
            sb.AppendLine("      if (isJson && wire?.response) {");
            sb.AppendLine("        data = fromWire(data, wire.response);");
            sb.AppendLine("      }");
        }

        if (zodRuntimeValidate)
        {
            // Validate JSON payloads only — text/blob responses aren't structured, so the
            // schema can't speak to them. The `safeParse` path returns the issues without
            // throwing; strictMode escalates to an exception so dev/CI can fail fast.
            sb.AppendLine();
            sb.AppendLine("      if (parseSchema && isJson && data !== undefined) {");
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
        sb.AppendLine("    // A 3xx fetch did not follow (300, 305, 306, or redirect: 'manual'): no error - its body, if any.");
        sb.AppendLine("    if (response.status >= 300 && response.status < 400) {");
        sb.AppendLine("      let data: unknown = undefined;");
        sb.AppendLine("      try {");
        sb.AppendLine("        if (contentType.includes('application/json') || contentType.includes('+json')) {");
        sb.Append("          data = ").Append(parseJson).AppendLine(";");
        sb.AppendLine("        } else if (contentType.startsWith('text/') || contentType.includes('application/xml') || contentType.includes('+xml')) {");
        sb.AppendLine("          const text = await response.text();");
        sb.AppendLine("          data = text.length > 0 ? text : undefined;");
        sb.AppendLine("        }");
        sb.AppendLine("      } catch {");
        sb.AppendLine("        data = undefined;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      return { status: 'redirection', data, response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // The error body is detected from the actual Content-Type (an operation's success responseType");
        sb.AppendLine("    // says nothing about its errors): a ProblemDetails / declared model / JSON string, or text (text/*, XML).");
        sb.AppendLine("    // Malformed or empty bodies are non-fatal - the message then falls back to the status text.");
        sb.AppendLine("    let errorBody: unknown = undefined;");
        sb.AppendLine("    try {");
        sb.AppendLine("      if (contentType.includes('application/json') || contentType.includes('+json')) {");
        sb.Append("        errorBody = ").Append(parseJson).AppendLine(";");
        sb.AppendLine("      } else if (contentType.startsWith('text/') || contentType.includes('application/xml') || contentType.includes('+xml')) {");
        sb.AppendLine("        const text = await response.text();");
        sb.AppendLine("        errorBody = text.length > 0 ? text : undefined;");
        sb.AppendLine("      }");
        sb.AppendLine("    } catch {");
        sb.AppendLine("      errorBody = undefined;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const message = errorMessage(response.status, response.statusText, errorBody);");
        sb.AppendLine();

        // With renaming, `body` is the declared error model under the models' names, while the message, the
        // ProblemDetails fields and the validation errors (keyed by the field names sent) come from the wire body.
        var errorBodyArguments = "errorBody";
        if (hasWireNames)
        {
            sb.AppendLine("    const shape = wire?.errors?.[String(response.status)] ?? wire?.errors?.['default'];");
            sb.AppendLine("    const body = shape ? fromWire(errorBody, shape) : errorBody;");
            sb.AppendLine();
            errorBodyArguments = "body, errorBody";
        }

        sb.AppendLine("    if (response.status === 400) {");
        sb.AppendLine("      // Always badRequest: a 400 without validation errors carries an empty errors map.");
        sb.AppendLine("      const errors = errorBody !== null && typeof errorBody === 'object' && typeof (errorBody as { errors?: unknown }).errors === 'object' && (errorBody as { errors?: unknown }).errors !== null");
        sb.AppendLine("        ? (errorBody as { errors: Record<string, string[]> }).errors");
        sb.AppendLine("        : {};");
        sb.AppendLine("      return {");
        sb.AppendLine("        status: 'badRequest',");
        sb.Append("        error: new ValidationError(response.status, response.statusText, message, errors, response, ").Append(errorBodyArguments).AppendLine("),");
        sb.AppendLine("        response,");
        sb.AppendLine("      };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    const apiError = new ApiError(response.status, response.statusText, message, response, ").Append(errorBodyArguments).AppendLine(");");
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
        sb.AppendLine("        return response.status >= 400 && response.status < 500");
        sb.AppendLine("          ? { status: 'clientError', error: apiError, response }");
        sb.AppendLine("          : { status: 'serverError', error: apiError, response };");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
    }
}