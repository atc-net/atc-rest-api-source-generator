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
        bool convertDates = false)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        AppendImports(sb);

        if (convertDates)
        {
            AppendDateReviverReplacer(sb);
        }

        AppendApiClientOptionsInterface(sb);
        AppendRequestOptionsInterface(sb);
        AppendApiClientClass(sb, convertDates);

        return sb.ToString();
    }

    private static void AppendImports(StringBuilder sb)
    {
        sb.AppendLine("import { ApiError } from '../errors/ApiError';");
        sb.AppendLine("import { ValidationError } from '../errors/ValidationError';");
        sb.AppendLine("import type { ApiResult } from '../types/ApiResult';");
        sb.AppendLine();
    }

    private static void AppendApiClientOptionsInterface(StringBuilder sb)
    {
        sb.AppendLine("export type FetchRequestInterceptor = (url: string, init: RequestInit) => RequestInit | Promise<RequestInit>;");
        sb.AppendLine("export type FetchResponseInterceptor = (response: Response) => Response | Promise<Response>;");
        sb.AppendLine();
        sb.AppendLine("export interface ApiClientOptions {");
        sb.AppendLine("  getAccessToken?: () => string | Promise<string>;");
        sb.AppendLine("  defaultHeaders?: Record<string, string>;");
        sb.AppendLine("  requestInterceptors?: FetchRequestInterceptor[];");
        sb.AppendLine("  responseInterceptors?: FetchResponseInterceptor[];");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendRequestOptionsInterface(StringBuilder sb)
    {
        sb.AppendLine("export interface RequestOptions {");
        sb.AppendLine("  body?: unknown;");
        sb.AppendLine("  query?: Record<string, string | number | boolean | undefined>;");
        sb.AppendLine("  headers?: Record<string, string>;");
        sb.AppendLine("  signal?: AbortSignal;");
        sb.AppendLine("  responseType?: 'json' | 'blob';");
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
        bool convertDates)
    {
        sb.AppendLine("export class ApiClient {");
        sb.AppendLine("  private readonly baseUrl: string;");
        sb.AppendLine("  private readonly options: ApiClientOptions;");
        sb.AppendLine();

        AppendConstructor(sb);
        AppendRequestMethod(sb, convertDates);
        AppendRequestStreamMethod(sb, convertDates);
        AppendBuildUrlMethod(sb);
        AppendGetHeadersMethod(sb);
        AppendHandleResponseMethod(sb, convertDates);

        sb.AppendLine("}");
    }

    private static void AppendConstructor(StringBuilder sb)
    {
        sb.AppendLine("  constructor(baseUrl: string, options?: ApiClientOptions) {");
        sb.AppendLine("    this.baseUrl = baseUrl.replace(/\\/+$/, '');");
        sb.AppendLine("    this.options = options ?? {};");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendRequestMethod(
        StringBuilder sb,
        bool convertDates)
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
        sb.AppendLine("    let response = await fetch(url, init);");
        sb.AppendLine();
        sb.AppendLine("    for (const interceptor of this.options.responseInterceptors ?? []) {");
        sb.AppendLine("      response = await interceptor(response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    return this.handleResponse<T>(response, options?.responseType);");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendRequestStreamMethod(
        StringBuilder sb,
        bool convertDates)
    {
        var jsonParse = convertDates ? "JSON.parse(trimmed, dateReviver)" : "JSON.parse(trimmed)";
        var jsonParseBuffer = convertDates ? "JSON.parse(buffer.trim(), dateReviver)" : "JSON.parse(buffer.trim())";

        sb.AppendLine("  async *requestStream<T>(method: string, path: string, options?: RequestOptions): AsyncGenerator<T> {");
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
        sb.AppendLine("    try {");
        sb.AppendLine("      while (true) {");
        sb.AppendLine("        const { done, value } = await reader.read();");
        sb.AppendLine("        if (done) break;");
        sb.AppendLine();
        sb.AppendLine("        buffer += decoder.decode(value, { stream: true });");
        sb.AppendLine("        const lines = buffer.split('\\n');");
        sb.AppendLine("        buffer = lines.pop() ?? '';");
        sb.AppendLine();
        sb.AppendLine("        for (const line of lines) {");
        sb.AppendLine("          const trimmed = line.trim();");
        sb.AppendLine("          if (trimmed.length === 0) continue;");
        sb.Append("          yield ").Append(jsonParse).AppendLine(" as T;");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      if (buffer.trim().length > 0) {");
        sb.Append("        yield ").Append(jsonParseBuffer).AppendLine(" as T;");
        sb.AppendLine("      }");
        sb.AppendLine("    } finally {");
        sb.AppendLine("      reader.releaseLock();");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendBuildUrlMethod(StringBuilder sb)
    {
        sb.AppendLine("  buildUrl(path: string, query?: Record<string, string | number | boolean | undefined>): string {");
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

    private static void AppendGetHeadersMethod(StringBuilder sb)
    {
        sb.AppendLine("  async getHeaders(extra?: Record<string, string>): Promise<Headers> {");
        sb.AppendLine("    const headers = new Headers(this.options.defaultHeaders);");
        sb.AppendLine();
        sb.AppendLine("    if (this.options.getAccessToken) {");
        sb.AppendLine("      const token = await this.options.getAccessToken();");
        sb.AppendLine("      headers.set('Authorization', `Bearer ${token}`);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (extra) {");
        sb.AppendLine("      for (const [key, value] of Object.entries(extra)) {");
        sb.AppendLine("        headers.set(key, value);");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    return headers;");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendHandleResponseMethod(
        StringBuilder sb,
        bool convertDates)
    {
        var parseJson = convertDates
            ? "JSON.parse(await response.text(), dateReviver)"
            : "await response.json()";

        sb.AppendLine("  private async handleResponse<T>(response: Response, responseType?: 'json' | 'blob'): Promise<ApiResult<T>> {");
        sb.AppendLine("    if (response.status === 204) {");
        sb.AppendLine("      return { status: 'noContent', response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const contentType = response.headers.get('Content-Type') ?? '';");
        sb.AppendLine("    const isJson = responseType ? responseType === 'json' : contentType.includes('application/json');");
        sb.AppendLine();
        sb.AppendLine("    if (response.ok) {");
        sb.Append("      const data = isJson ? ").Append(parseJson).AppendLine(" : await response.blob();");
        sb.AppendLine("      const status = response.status === 201 ? 'created' as const : 'ok' as const;");
        sb.AppendLine("      return { status, data: data as T, response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.Append("    const errorBody = isJson ? ").Append(parseJson).AppendLine(" : null;");
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
        sb.AppendLine("      case 404:");
        sb.AppendLine("        return { status: 'notFound', error: apiError, response };");
        sb.AppendLine("      case 409:");
        sb.AppendLine("        return { status: 'conflict', error: apiError, response };");
        sb.AppendLine("      default:");
        sb.AppendLine("        return { status: 'serverError', error: apiError, response };");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
    }
}