namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates the Axios-based ApiClient.ts with interceptor support and automatic JSON parsing.
/// </summary>
public static class TypeScriptAxiosApiClientExtractor
{
    /// <summary>
    /// Generates the content for ApiClient.ts using Axios.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated file header.</param>
    /// <param name="hasRetry">When true, the spec declares retry (x-retry-*): the client retries through helpers/retryInterceptor.ts.</param>
    /// <param name="zodRuntimeValidate">When true, responses are validated against their Zod schema.</param>
    /// <param name="hasWireNames">
    /// When true, the TypeScript models' property names differ from the API's for some keys: bodies, responses,
    /// error bodies and stream items are renamed through <c>helpers/wireNames.ts</c>.
    /// </param>
    /// <returns>The TypeScript file content.</returns>
    public static string Generate(
        string? headerContent,
        bool hasRetry = false,
        bool zodRuntimeValidate = false,
        bool hasWireNames = false)
    {
        var sb = new StringBuilder();

        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        AppendImports(sb, hasRetry, zodRuntimeValidate, hasWireNames);

        AppendUnparseableBodyClass(sb);
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
        sb.AppendLine("import axios from 'axios';");
        sb.AppendLine("import type { AxiosInstance, AxiosResponse, InternalAxiosRequestConfig } from 'axios';");
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
            sb.AppendLine("import type { ZodTypeAny } from 'zod';");
        }

        sb.AppendLine();
    }

    private static void AppendUnparseableBodyClass(StringBuilder sb)
    {
        // Sentinel produced by transformResponse when a JSON body fails to parse. This is the
        // only reliable "JSON.parse failed" signal — a successfully parsed JSON string body is
        // itself a JS string, so typeof-based detection cannot tell the two apart. Carrying the
        // failure as a distinct type lets handleResponse detect it with an instanceof check.
        sb.AppendLine("class UnparseableBody {");
        sb.AppendLine("  constructor(public readonly raw: string) {}");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendApiClientOptionsInterface(
        StringBuilder sb,
        bool hasRetry)
    {
        sb.AppendLine("export type AxiosRequestInterceptor = (config: InternalAxiosRequestConfig) => InternalAxiosRequestConfig | Promise<InternalAxiosRequestConfig>;");
        sb.AppendLine("export type AxiosResponseInterceptor = (response: AxiosResponse) => AxiosResponse | Promise<AxiosResponse>;");
        sb.AppendLine();
        sb.AppendLine("export interface ApiClientOptions {");
        sb.AppendLine("  getAccessToken?: () => string | Promise<string>;");
        sb.AppendLine("  defaultHeaders?: Record<string, string>;");
        sb.AppendLine("  requestInterceptors?: AxiosRequestInterceptor[];");
        sb.AppendLine("  responseInterceptors?: AxiosResponseInterceptor[];");

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
        sb.AppendLine("  private readonly client: AxiosInstance;");
        sb.AppendLine("  private readonly options: ApiClientOptions;");

        if (hasRetry)
        {
            sb.AppendLine("  private readonly retryPolicy: RetryPolicy | false;");
        }

        if (zodRuntimeValidate)
        {
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
        AppendHandleResponseMethod(sb, zodRuntimeValidate, hasWireNames);

        sb.AppendLine("}");
    }

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

        sb.AppendLine("    this.client = axios.create({");
        sb.AppendLine("      baseURL: this.baseUrl,");
        sb.AppendLine("      validateStatus: () => true,");

        // Suppress axios 1.x bracket notation (tags[]=a) and emit repeated keys (tags=a&tags=b)
        // so ASP.NET Core's default model binder can bind array query params correctly.
        sb.AppendLine("      paramsSerializer: { indexes: null },");

        // transformResponse is always emitted: it is the single place
        // where a JSON parse failure is observable, so it must own the parse. It is a function (not
        // an arrow) so `this` is the request config: responseType 'json' (a JSON operation) parses the
        // body whatever the content-type says, and responseType 'text' returns it verbatim even for a
        // JSON content-type. Otherwise parsing is gated on the content-type: only JSON bodies
        // (application/json and +json suffixes such as application/problem+json) are parsed, and only
        // they can yield the UnparseableBody sentinel. Text bodies (text/plain, application/xml, ...)
        // are returned verbatim so they never regress into a parse error.
        const string jsonParse = "JSON.parse(data)";

        sb.AppendLine("      transformResponse: [function (this: InternalAxiosRequestConfig, data: unknown, headers?: Record<string, string>) {");
        sb.AppendLine("        if (typeof data !== 'string' || data.length === 0) return data;");
        sb.AppendLine("        if (this.responseType === 'text') return data;");
        sb.AppendLine("        const contentType = String(headers?.['content-type'] ?? headers?.['Content-Type'] ?? '').toLowerCase();");
        sb.AppendLine("        if (this.responseType !== 'json' && !contentType.includes('application/json') && !contentType.includes('+json')) return data;");
        sb.Append("        try { return ").Append(jsonParse).AppendLine("; } catch { return new UnparseableBody(data); }");
        sb.AppendLine("      }],");

        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    if (this.options.defaultHeaders) {");
        sb.AppendLine("      for (const [key, value] of Object.entries(this.options.defaultHeaders)) {");
        sb.AppendLine("        this.client.defaults.headers.common[key] = value;");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (this.options.getAccessToken) {");
        sb.AppendLine("      const getToken = this.options.getAccessToken;");
        sb.AppendLine("      this.client.interceptors.request.use(async (config) => {");
        sb.AppendLine("        const token = await getToken();");
        sb.AppendLine("        config.headers.Authorization = `Bearer ${token}`;");
        sb.AppendLine("        return config;");
        sb.AppendLine("      });");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    for (const interceptor of this.options.requestInterceptors ?? []) {");
        sb.AppendLine("      this.client.interceptors.request.use(interceptor);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    for (const interceptor of this.options.responseInterceptors ?? []) {");
        sb.AppendLine("      this.client.interceptors.response.use(interceptor);");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendRequestMethod(
        StringBuilder sb,
        bool hasRetry,
        bool zodRuntimeValidate,
        bool hasWireNames)
    {
        var jsonBody = hasWireNames ? "toWire(options.body, options.wire?.body)" : "options.body";
        sb.AppendLine("  async request<T>(method: string, path: string, options?: RequestOptions): Promise<ApiResult<T>> {");
        sb.AppendLine("    let data: unknown;");
        sb.AppendLine("    const headers: Record<string, string> = {};");
        sb.AppendLine();
        sb.AppendLine("    if (options?.body !== undefined) {");
        sb.AppendLine("      if (options.body instanceof FormData) {");
        sb.AppendLine("        // Named, not left to the adapter: axios's fetch adapter in Node labels a FormData form-urlencoded.");
        sb.AppendLine("        // Axios adds the boundary; in a browser it drops the header and the browser sets it.");
        sb.AppendLine("        headers['Content-Type'] = 'multipart/form-data';");
        sb.AppendLine("        data = options.body;");
        sb.AppendLine("      } else if (options.body instanceof URLSearchParams) {");
        sb.AppendLine("        headers['Content-Type'] = 'application/x-www-form-urlencoded';");
        sb.AppendLine("        data = options.body;");
        sb.AppendLine("      } else if (options.body instanceof Blob) {");
        sb.AppendLine("        const fileType = options.contentType ?? (options.body.type || 'application/octet-stream');");
        sb.AppendLine("        headers['Content-Type'] = fileType;");
        sb.AppendLine("        // Re-typed: an adapter may send the Blob's own type (Node's http adapter does)");
        sb.AppendLine("        data = options.body.type === fileType ? options.body : new Blob([options.body], { type: fileType });");

        sb.AppendLine("      } else {");
        sb.AppendLine("        headers['Content-Type'] = options.contentType ?? 'application/json';");
        sb.Append("        data = ").Append(jsonBody).AppendLine(";");

        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Skip undefined values — the generated client method passes optional header
        // params verbatim, and axios should not see literal undefined in the headers map.
        sb.AppendLine("    if (options?.headers) {");
        sb.AppendLine("      for (const [key, value] of Object.entries(options.headers)) {");
        sb.AppendLine("        if (value !== undefined) {");
        sb.AppendLine("          headers[key] = String(value);");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();

        if (hasRetry)
        {
            sb.AppendLine("    // doRequest accepts an optional override signal so the retry path can hand each");
            sb.AppendLine("    // attempt a fresh AbortSignal — that way policy.timeoutMs actually cancels axios.");
            sb.AppendLine("    const doRequest = (attemptSignal?: AbortSignal) => this.client.request<T>({");
            sb.AppendLine("      method,");
            sb.AppendLine("      url: path,");
            sb.AppendLine("      data,");
            sb.AppendLine("      params: options?.query,");
            sb.AppendLine("      headers,");
            sb.AppendLine("      signal: attemptSignal ?? options?.signal,");
            sb.AppendLine("      responseType: options?.responseType === 'blob' ? 'arraybuffer' : options?.responseType,");
            sb.AppendLine("    });");
            sb.AppendLine();
            sb.AppendLine("    let response: AxiosResponse<T> | undefined;");
            sb.AppendLine("    const retryPolicy = resolveRetryPolicy(method, this.retryPolicy, options?.retry);");
            sb.AppendLine("    if (retryPolicy) {");
            sb.AppendLine("      // Use retryWithBackoff with a fetch-compatible wrapper.");
            sb.AppendLine("      // The wrapper captures the AxiosResponse so it can be handed to handleResponse below,");
            sb.AppendLine("      // and passes Retry-After on so a 429 back-off (and its cap) can see it.");
            sb.AppendLine("      const fetchWrapper = async (attemptSignal: AbortSignal): Promise<Response> => {");
            sb.AppendLine("        response = await doRequest(attemptSignal);");
            sb.AppendLine("        const retryAfter = response.headers?.['retry-after'];");
            sb.AppendLine("        return new Response(null, {");
            sb.AppendLine("          status: response.status,");
            sb.AppendLine("          headers: retryAfter !== undefined && retryAfter !== null ? { 'Retry-After': String(retryAfter) } : undefined,");
            sb.AppendLine("        });");
            sb.AppendLine("      };");
            sb.AppendLine("      await retryWithBackoff(fetchWrapper, retryPolicy, options?.signal);");
            sb.AppendLine("    } else {");
            sb.AppendLine("      response = await doRequest();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    if (!response) {");
            sb.AppendLine("      throw new Error('retryWithBackoff resolved without executing the request');");
            sb.AppendLine("    }");
        }
        else
        {
            sb.AppendLine("    const response = await this.client.request<T>({");
            sb.AppendLine("      method,");
            sb.AppendLine("      url: path,");
            sb.AppendLine("      data,");
            sb.AppendLine("      params: options?.query,");
            sb.AppendLine("      headers,");
            sb.AppendLine("      signal: options?.signal,");
            sb.AppendLine("      responseType: options?.responseType === 'blob' ? 'arraybuffer' : options?.responseType,");
            sb.AppendLine("    });");
        }

        sb.AppendLine();

        // A file is asked for as bytes - every adapter keeps them (XHR, fetch, and Node's http adapter, which decodes a
        // 'blob' as text) - and handed on as a Blob of the response's media type; an error's bytes as text, read like
        // any error body.
        sb.AppendLine("    if (Object.prototype.toString.call(response.data) === '[object ArrayBuffer]' || ArrayBuffer.isView(response.data)) {");
        sb.AppendLine("      const bytes = response.data as unknown as ArrayBuffer;");
        sb.AppendLine("      const fileType = String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? '');");
        sb.AppendLine("      (response as { data: unknown }).data = response.status >= 200 && response.status < 300 && typeof Blob !== 'undefined'");
        sb.AppendLine("        ? new Blob([bytes], fileType ? { type: fileType } : undefined)");
        sb.AppendLine("        : new TextDecoder().decode(bytes);");
        sb.AppendLine("    }");
        sb.AppendLine();

        var wireArgument = hasWireNames ? ", options?.wire" : string.Empty;
        if (zodRuntimeValidate)
        {
            sb.Append("    return fitToArms(await this.handleResponse<T>(response, options?.parseSchema").Append(wireArgument).AppendLine("), options?.arms);");
        }
        else
        {
            sb.Append("    return fitToArms(await this.handleResponse<T>(response").Append(wireArgument).AppendLine("), options?.arms);");
        }

        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendRequestStreamMethod(
        StringBuilder sb,
        bool hasWireNames)
    {
        const string jsonParse = "JSON.parse(buffer.substring(objStart, objEnd + 1))";
        const string multipartParse = "JSON.parse(bodyText)";

        sb.AppendLine("  // Streaming uses native fetch — Axios doesn't natively support ReadableStream iteration.");
        sb.AppendLine("  // Auth and default headers are applied manually; Axios interceptors do not apply to streaming requests.");

        // With renaming, requestStream renames each item this reader yields.
        sb.AppendLine(hasWireNames
            ? "  private async *readStream<T>(method: string, path: string, options?: RequestOptions, framing: StreamFraming = 'json-array'): AsyncGenerator<T> {"
            : "  async *requestStream<T>(method: string, path: string, options?: RequestOptions, framing: StreamFraming = 'json-array'): AsyncGenerator<T> {");
        sb.AppendLine("    const url = this.buildUrl(path, options?.query);");
        sb.AppendLine("    const headers = new Headers(this.options.defaultHeaders);");
        sb.AppendLine();
        sb.AppendLine("    if (this.options.getAccessToken) {");
        sb.AppendLine("      const token = await this.options.getAccessToken();");
        sb.AppendLine("      headers.set('Authorization', `Bearer ${token}`);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (options?.headers) {");
        sb.AppendLine("      for (const [key, value] of Object.entries(options.headers)) {");
        sb.AppendLine("        if (value !== undefined) {");
        sb.AppendLine("          headers.set(key, String(value));");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        TypeScriptOperationHelper.AppendStreamRequestBody(sb, hasWireNames);
        sb.AppendLine("    const response = await fetch(url, {");
        sb.AppendLine("      method,");
        sb.AppendLine("      headers,");
        sb.AppendLine("      body: streamBody,");
        sb.AppendLine("      signal: options?.signal,");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    if (!response.ok) {");
        sb.AppendLine("      // Read the error body like the non-streaming path: a ProblemDetails / model / JSON string, or text.");
        sb.AppendLine("      const errorContentType = (response.headers.get('Content-Type') ?? '').toLowerCase();");
        sb.AppendLine("      let errorBody: unknown = undefined;");
        sb.AppendLine("      try {");
        sb.AppendLine("        if (errorContentType.includes('application/json') || errorContentType.includes('+json')) {");
        sb.AppendLine("          errorBody = await response.json();");
        sb.AppendLine("        } else if (errorContentType.startsWith('text/') || errorContentType.includes('application/xml') || errorContentType.includes('+xml')) {");
        sb.AppendLine("          const text = await response.text();");
        sb.AppendLine("          errorBody = text.length > 0 ? text : undefined;");
        sb.AppendLine("        }");
        sb.AppendLine("      } catch {");
        sb.AppendLine("        errorBody = undefined;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      throw new ApiError(response.status, response.statusText, errorMessage(response.status, response.statusText, errorBody), undefined, errorBody);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const reader = response.body?.getReader();");
        sb.AppendLine("    if (!reader) {");
        sb.AppendLine("      throw new ApiError(0, 'NoBody', 'Response body is empty');");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const decoder = new TextDecoder();");
        sb.AppendLine("    let buffer = '';");
        sb.AppendLine();
        sb.AppendLine("    if (framing === 'sse') {");
        sb.AppendLine("      yield* parseEventStream<T>(reader, decoder);");
        sb.AppendLine("      return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (framing === 'multipart') {");
        sb.AppendLine("      const contentType = response.headers.get('content-type') ?? '';");
        sb.AppendLine("      const m = /boundary=(\"?)([^\";]+)\\1/i.exec(contentType);");
        sb.AppendLine("      const delimiter = '--' + (m ? m[2] : '');");
        sb.AppendLine("      let buf = '';");
        sb.AppendLine("      let finished = false;");
        sb.AppendLine("      try {");
        sb.AppendLine("        while (true) {");
        sb.AppendLine("          const { done, value } = await reader.read();");
        sb.AppendLine("          if (done) { finished = true; break; }");
        sb.AppendLine("          buf += decoder.decode(value, { stream: true });");
        sb.AppendLine("          // Discard preamble (everything before the first delimiter boundary).");
        sb.AppendLine("          const first = buf.indexOf(delimiter);");
        sb.AppendLine("          if (first === -1) { buf = ''; continue; }");
        sb.AppendLine("          if (first > 0) buf = buf.substring(first);");
        sb.AppendLine("          // Yield each complete part; buf always starts at a delimiter after this loop.");
        sb.AppendLine("          while (buf.startsWith(delimiter)) {");
        sb.AppendLine("            const partStart = delimiter.length;");
        sb.AppendLine("            const next = buf.indexOf(delimiter, partStart);");
        sb.AppendLine("            if (next === -1) break;");
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
        sb.AppendLine("    let finished = false;");
        sb.AppendLine("    try {");
        sb.AppendLine("      while (true) {");
        sb.AppendLine("        const { done, value } = await reader.read();");
        sb.AppendLine("        if (done) { finished = true; break; }");
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
        sb.AppendLine("      await releaseReader(reader, finished);");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendBuildUrlMethod(StringBuilder sb)
    {
        sb.AppendLine("  private buildUrl(path: string, query?: Record<string, string | number | boolean | (string | number | boolean)[] | undefined>): string {");
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

    private static void AppendHandleResponseMethod(
        StringBuilder sb,
        bool zodRuntimeValidate,
        bool hasWireNames)
    {
        var wireParameter = hasWireNames ? ", wire?: WireShapes" : string.Empty;
        var signature = zodRuntimeValidate
            ? "  private async handleResponse<T>(response: AxiosResponse<T>, parseSchema?: ZodTypeAny" + wireParameter + "): Promise<ApiResult<T>> {"
            : "  private async handleResponse<T>(response: AxiosResponse<T>" + wireParameter + "): Promise<ApiResult<T>> {";
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
        sb.AppendLine("    if (response.status >= 200 && response.status < 300) {");
        sb.AppendLine("      // A JSON body that failed to parse is carried as an UnparseableBody sentinel by");
        sb.AppendLine("      // transformResponse (the only place the parse outcome is known). Surface it as a");
        sb.AppendLine("      // discriminated 'parseError' instead of pretending the response succeeded. A JSON");
        sb.AppendLine("      // string body that parsed fine (e.g. \"token\") is a plain string, not a sentinel.");
        sb.AppendLine("      if (response.data instanceof UnparseableBody) {");
        sb.AppendLine("        return {");
        sb.AppendLine("          status: 'parseError',");
        sb.AppendLine("          error: new Error('Response body could not be parsed as JSON'),");
        sb.AppendLine("          response,");
        sb.AppendLine("        };");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      // An empty body is no data (a text operation keeps its empty string).");
        sb.AppendLine("      if ((response.data as unknown) === '' && response.config?.responseType !== 'text') {");
        sb.AppendLine("        (response as { data: unknown }).data = undefined;");
        sb.AppendLine("      }");

        if (hasWireNames)
        {
            // The API's property names -> the models' (before a schema check, which speaks the models' names).
            // Text and blob bodies are no plain objects, so they pass through.
            sb.AppendLine();
            sb.AppendLine("      if (wire?.response) {");
            sb.AppendLine("        response = { ...response, data: fromWire<T>(response.data, wire.response) };");
            sb.AppendLine("      }");
        }

        if (zodRuntimeValidate)
        {
            // Axios already parsed the response (response.data); we validate that against
            // the schema. JSON-only — text/blob bodies don't have a structured schema.
            sb.AppendLine();
            sb.AppendLine("      const contentType = String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? '');");
            sb.AppendLine("      const expectsJson = contentType.includes('application/json') || contentType.includes('+json');");
            sb.AppendLine("      if (parseSchema && expectsJson && response.data !== undefined) {");
            sb.AppendLine("        const parsed = parseSchema.safeParse(response.data);");
            sb.AppendLine("        if (!parsed.success) {");
            sb.AppendLine("          if (this.strictMode) {");
            sb.AppendLine("            const issuesSummary = parsed.error.issues.map(i => `${i.path.join('.')}: ${i.message}`).join('; ');");
            sb.AppendLine("            throw new Error(`Schema mismatch: ${issuesSummary}`);");
            sb.AppendLine("          }");
            sb.AppendLine();
            sb.AppendLine("          return { status: 'schemaMismatch', issues: parsed.error.issues, data: response.data, response };");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        response = { ...response, data: parsed.data as T };");
            sb.AppendLine("      }");
            sb.AppendLine();
        }

        sb.AppendLine("      const status = response.status === 201");
        sb.AppendLine("        ? 'created' as const");
        sb.AppendLine("        : response.status === 202");
        sb.AppendLine("          ? 'accepted' as const");
        sb.AppendLine("          : 'ok' as const;");
        sb.AppendLine("      return { status, data: response.data, response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // A 3xx axios did not follow (300, 305, 306, or maxRedirects: 0): no error - its body, if any.");
        sb.AppendLine("    if (response.status >= 300 && response.status < 400) {");
        sb.AppendLine("      const data = response.data instanceof UnparseableBody || response.data === '' ? undefined : response.data;");
        sb.AppendLine("      return { status: 'redirection', data, response };");
        sb.AppendLine("    }");
        sb.AppendLine();

        // The error body: a ProblemDetails / declared model / JSON string (already parsed by
        // transformResponse), or text. A JSON body left as a string - the operation asked for
        // responseType 'text', or a download's error arrived as a Blob - is parsed here; a malformed one
        // (the UnparseableBody sentinel) or an empty one yields no body, so the message falls back to the status text.
        sb.AppendLine("    let errorBody: unknown = response.data instanceof UnparseableBody ? undefined : response.data;");
        sb.AppendLine("    let errorBodyIsRawText = response.config?.responseType === 'text' || response.config?.responseType === 'arraybuffer';");
        sb.AppendLine("    if (typeof Blob !== 'undefined' && errorBody instanceof Blob) {");
        sb.AppendLine("      // A download's error arrives as a Blob (responseType 'blob'): read it like any other error body.");
        sb.AppendLine("      errorBody = await errorBody.text();");
        sb.AppendLine("      errorBodyIsRawText = true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (typeof errorBody === 'string') {");
        sb.AppendLine("      const errorContentType = String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? '').toLowerCase();");
        sb.AppendLine("      if (errorBody.length === 0) {");
        sb.AppendLine("        errorBody = undefined;");
        sb.AppendLine("      } else if (errorContentType.includes('application/json') || errorContentType.includes('+json')) {");
        sb.AppendLine("        // Only a responseType 'text' request or a Blob left a JSON body unparsed; otherwise a string here is the");
        sb.AppendLine("        // parsed value of a JSON-string body (\"42\" stays the string '42').");
        sb.AppendLine("        if (errorBodyIsRawText) {");
        sb.AppendLine("          try {");
        sb.AppendLine("            errorBody = JSON.parse(errorBody);");
        sb.AppendLine("          } catch {");
        sb.AppendLine("            errorBody = undefined;");
        sb.AppendLine("          }");
        sb.AppendLine("        }");
        sb.AppendLine("      } else if (!(errorContentType.startsWith('text/') || errorContentType.includes('application/xml') || errorContentType.includes('+xml'))) {");
        sb.AppendLine("        // Neither JSON nor text (e.g. application/octet-stream, or no Content-Type): no body, as in the Fetch client.");
        sb.AppendLine("        errorBody = undefined;");
        sb.AppendLine("      }");
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