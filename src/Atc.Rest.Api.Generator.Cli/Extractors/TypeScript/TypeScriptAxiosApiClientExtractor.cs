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
        sb.AppendLine("import axios from 'axios';");
        sb.AppendLine("import type { AxiosInstance, AxiosResponse, InternalAxiosRequestConfig } from 'axios';");
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
            sb.AppendLine("import type { ZodTypeAny } from 'zod';");
        }

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
        bool zodRuntimeValidate)
    {
        sb.AppendLine("export interface RequestOptions {");
        sb.AppendLine("  body?: unknown;");
        sb.AppendLine("  query?: Record<string, string | number | boolean | undefined>;");
        sb.AppendLine("  headers?: Record<string, string | number | boolean | undefined>;");
        sb.AppendLine("  signal?: AbortSignal;");
        sb.AppendLine("  responseType?: 'json' | 'blob' | 'text';");
        if (zodRuntimeValidate)
        {
            sb.AppendLine("  parseSchema?: ZodTypeAny;");
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendDateReviverReplacer(StringBuilder sb)
    {
        sb.AppendLine("const ISO_DATE_RE = /^\\d{4}-\\d{2}-\\d{2}(T\\d{2}:\\d{2})/;");
        sb.AppendLine();
        sb.AppendLine("function dateReviver(_key: string, value: unknown): unknown {");
        sb.AppendLine("  if (typeof value === 'string' && ISO_DATE_RE.test(value)) {");
        sb.AppendLine("    return new Date(value);");
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

        AppendConstructor(sb, convertDates, hasRetry);

        if (zodRuntimeValidate)
        {
            AppendSetStrictModeMethod(sb);
        }

        AppendRequestMethod(sb, convertDates, hasRetry, zodRuntimeValidate);
        AppendRequestStreamMethod(sb, convertDates);
        AppendBuildUrlMethod(sb);
        AppendHandleResponseMethod(sb, zodRuntimeValidate);

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
        bool convertDates,
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

        if (convertDates)
        {
            sb.AppendLine("      transformResponse: [(data: string) => {");
            sb.AppendLine("        if (typeof data !== 'string') return data;");
            sb.AppendLine("        try { return JSON.parse(data, dateReviver); } catch { return data; }");
            sb.AppendLine("      }],");
            sb.AppendLine("      transformRequest: [(data: unknown, headers: Record<string, string>) => {");
            sb.AppendLine("        if (data instanceof FormData || data instanceof Blob) return data;");
            sb.AppendLine("        if (data !== undefined) headers['Content-Type'] = 'application/json';");
            sb.AppendLine("        return data !== undefined ? JSON.stringify(data, dateReplacer) : data;");
            sb.AppendLine("      }],");
        }

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
        bool convertDates,
        bool hasRetry,
        bool zodRuntimeValidate)
    {
        sb.AppendLine("  async request<T>(method: string, path: string, options?: RequestOptions): Promise<ApiResult<T>> {");
        sb.AppendLine("    let data: unknown;");
        sb.AppendLine("    const headers: Record<string, string> = {};");
        sb.AppendLine();
        sb.AppendLine("    if (options?.body !== undefined) {");
        sb.AppendLine("      if (options.body instanceof FormData) {");
        sb.AppendLine("        data = options.body;");
        sb.AppendLine("      } else if (options.body instanceof Blob) {");
        sb.AppendLine("        headers['Content-Type'] = 'application/octet-stream';");
        sb.AppendLine("        data = options.body;");

        if (convertDates)
        {
            // transformRequest handles Content-Type and JSON.stringify with dateReplacer
            sb.AppendLine("      } else {");
            sb.AppendLine("        data = options.body;");
        }
        else
        {
            sb.AppendLine("      } else {");
            sb.AppendLine("        headers['Content-Type'] = 'application/json';");
            sb.AppendLine("        data = options.body;");
        }

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
            sb.AppendLine("      responseType: options?.responseType === 'blob' ? 'blob' : options?.responseType === 'text' ? 'text' : 'json',");
            sb.AppendLine("    });");
            sb.AppendLine();
            sb.AppendLine("    let response: AxiosResponse<T> | undefined;");
            sb.AppendLine("    if (this.retryPolicy) {");
            sb.AppendLine("      // Use retryWithBackoff with a fetch-compatible wrapper.");
            sb.AppendLine("      // The wrapper captures the AxiosResponse so it can be handed to handleResponse below.");
            sb.AppendLine("      const fetchWrapper = async (attemptSignal: AbortSignal): Promise<Response> => {");
            sb.AppendLine("        response = await doRequest(attemptSignal);");
            sb.AppendLine("        return new Response(null, { status: response.status });");
            sb.AppendLine("      };");
            sb.AppendLine("      await retryWithBackoff(fetchWrapper, this.retryPolicy, options?.signal);");
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
            sb.AppendLine("      responseType: options?.responseType === 'blob' ? 'blob' : options?.responseType === 'text' ? 'text' : 'json',");
            sb.AppendLine("    });");
        }

        sb.AppendLine();

        if (zodRuntimeValidate)
        {
            sb.AppendLine("    return this.handleResponse<T>(response, options?.parseSchema);");
        }
        else
        {
            sb.AppendLine("    return this.handleResponse<T>(response);");
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

        sb.AppendLine("  // Streaming uses native fetch — Axios doesn't natively support ReadableStream iteration.");
        sb.AppendLine("  // Auth and default headers are applied manually; Axios interceptors do not apply to streaming requests.");
        sb.AppendLine("  async *requestStream<T>(method: string, path: string, options?: RequestOptions): AsyncGenerator<T> {");
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
        sb.AppendLine("    const response = await fetch(url, {");
        sb.AppendLine("      method,");
        sb.AppendLine("      headers,");
        sb.AppendLine("      signal: options?.signal,");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    if (!response.ok) {");
        sb.AppendLine("      throw new ApiError(response.status, response.statusText, 'Stream request failed');");
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
        sb.AppendLine("  private buildUrl(path: string, query?: Record<string, string | number | boolean | undefined>): string {");
        sb.AppendLine("    const url = new URL(`${this.baseUrl}${path}`);");
        sb.AppendLine("    if (query) {");
        sb.AppendLine("      for (const [key, value] of Object.entries(query)) {");
        sb.AppendLine("        if (value !== undefined) {");
        sb.AppendLine("          url.searchParams.set(key, String(value));");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("    return url.toString();");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendHandleResponseMethod(
        StringBuilder sb,
        bool zodRuntimeValidate)
    {
        var signature = zodRuntimeValidate
            ? "  private handleResponse<T>(response: AxiosResponse<T>, parseSchema?: ZodTypeAny): ApiResult<T> {"
            : "  private handleResponse<T>(response: AxiosResponse<T>): ApiResult<T> {";
        sb.AppendLine(signature);
        sb.AppendLine("    if (response.status === 204) {");
        sb.AppendLine("      return { status: 'noContent', response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (response.status >= 200 && response.status < 300) {");
        sb.AppendLine("      // Axios with responseType: 'json' falls back to the raw string when JSON.parse");
        sb.AppendLine("      // fails. Detect that case (text body where JSON was expected) and surface it");
        sb.AppendLine("      // as a discriminated 'parseError' instead of pretending the response succeeded.");
        sb.AppendLine("      const contentType = String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? '');");
        sb.AppendLine("      const expectsJson = contentType.includes('application/json');");
        sb.AppendLine("      if (expectsJson && typeof response.data === 'string' && (response.data as string).length > 0) {");
        sb.AppendLine("        return {");
        sb.AppendLine("          status: 'parseError',");
        sb.AppendLine("          error: new Error('Response body could not be parsed as JSON'),");
        sb.AppendLine("          response,");
        sb.AppendLine("        };");
        sb.AppendLine("      }");

        if (zodRuntimeValidate)
        {
            // Axios already parsed the response (response.data); we validate that against
            // the schema. JSON-only — text/blob bodies don't have a structured schema.
            sb.AppendLine();
            sb.AppendLine("      if (parseSchema && expectsJson) {");
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
        sb.AppendLine("    const errorBody = response.data as Record<string, unknown> | null;");
        sb.AppendLine("    const message = (errorBody?.title ?? errorBody?.message ?? response.statusText) as string;");
        sb.AppendLine();
        sb.AppendLine("    if (response.status === 400 && errorBody?.errors) {");
        sb.AppendLine("      return {");
        sb.AppendLine("        status: 'badRequest',");
        sb.AppendLine("        error: new ValidationError(");
        sb.AppendLine("          response.status, response.statusText, message,");
        sb.AppendLine("          errorBody.errors as Record<string, string[]>, response,");
        sb.AppendLine("        ),");
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