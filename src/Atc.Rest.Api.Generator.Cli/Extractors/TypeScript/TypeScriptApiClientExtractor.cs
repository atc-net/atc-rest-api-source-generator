namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates the base ApiClient.ts with fetch wrapper, interfaces, and response handling.
/// </summary>
public static class TypeScriptApiClientExtractor
{
    /// <summary>
    /// Generates the content for ApiClient.ts.
    /// </summary>
    /// <param name="headerContent">Optional auto-generated file header.</param>
    /// <returns>The TypeScript file content.</returns>
    public static string Generate(string? headerContent)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        AppendImports(sb);
        AppendApiClientOptionsInterface(sb);
        AppendRequestOptionsInterface(sb);
        AppendApiClientClass(sb);

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
        sb.AppendLine("export interface ApiClientOptions {");
        sb.AppendLine("  getAccessToken?: () => string | Promise<string>;");
        sb.AppendLine("  defaultHeaders?: Record<string, string>;");
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
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendApiClientClass(StringBuilder sb)
    {
        sb.AppendLine("export class ApiClient {");
        sb.AppendLine("  private readonly baseUrl: string;");
        sb.AppendLine("  private readonly options: ApiClientOptions;");
        sb.AppendLine();

        AppendConstructor(sb);
        AppendRequestMethod(sb);
        AppendRequestStreamMethod(sb);
        AppendBuildUrlMethod(sb);
        AppendGetHeadersMethod(sb);
        AppendHandleResponseMethod(sb);

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

    private static void AppendRequestMethod(StringBuilder sb)
    {
        sb.AppendLine("  async request<T>(method: string, path: string, options?: RequestOptions): Promise<ApiResult<T>> {");
        sb.AppendLine("    const url = this.buildUrl(path, options?.query);");
        sb.AppendLine("    const headers = await this.getHeaders(options?.headers);");
        sb.AppendLine();
        sb.AppendLine("    let fetchBody: BodyInit | undefined;");
        sb.AppendLine("    if (options?.body !== undefined) {");
        sb.AppendLine("      if (options.body instanceof FormData) {");
        sb.AppendLine("        fetchBody = options.body;");
        sb.AppendLine("      } else {");
        sb.AppendLine("        headers.set('Content-Type', 'application/json');");
        sb.AppendLine("        fetchBody = JSON.stringify(options.body);");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const response = await fetch(url, {");
        sb.AppendLine("      method,");
        sb.AppendLine("      headers,");
        sb.AppendLine("      body: fetchBody,");
        sb.AppendLine("      signal: options?.signal,");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    return this.handleResponse<T>(response);");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendRequestStreamMethod(StringBuilder sb)
    {
        sb.AppendLine("  async *requestStream<T>(method: string, path: string, options?: RequestOptions): AsyncGenerator<T> {");
        sb.AppendLine("    const url = this.buildUrl(path, options?.query);");
        sb.AppendLine("    const headers = await this.getHeaders(options?.headers);");
        sb.AppendLine();
        sb.AppendLine("    const response = await fetch(url, {");
        sb.AppendLine("      method,");
        sb.AppendLine("      headers,");
        sb.AppendLine("      signal: options?.signal,");
        sb.AppendLine("    });");
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
        sb.AppendLine("          yield JSON.parse(trimmed) as T;");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      if (buffer.trim().length > 0) {");
        sb.AppendLine("        yield JSON.parse(buffer.trim()) as T;");
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

    private static void AppendHandleResponseMethod(StringBuilder sb)
    {
        sb.AppendLine("  private async handleResponse<T>(response: Response): Promise<ApiResult<T>> {");
        sb.AppendLine("    if (response.status === 204) {");
        sb.AppendLine("      return { status: 'noContent', response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const contentType = response.headers.get('Content-Type') ?? '';");
        sb.AppendLine("    const isJson = contentType.includes('application/json');");
        sb.AppendLine();
        sb.AppendLine("    if (response.ok) {");
        sb.AppendLine("      const data = isJson ? await response.json() : await response.blob();");
        sb.AppendLine("      const status = response.status === 201 ? 'created' as const : 'ok' as const;");
        sb.AppendLine("      return { status, data: data as T, response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const errorBody = isJson ? await response.json() : null;");
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