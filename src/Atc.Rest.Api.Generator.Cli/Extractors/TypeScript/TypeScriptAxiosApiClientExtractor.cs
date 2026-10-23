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
    /// <param name="convertDates">When true (<c>--convert-dates</c>), the file exports <c>dateOnly</c>, which the segment clients write a <c>format: date</c> parameter with.</param>
    /// <param name="hasXml">
    /// When true, the specification has XML bodies: the <c>xml</c> request option (Accept, an XML request body), an XML
    /// success body read by the operation's mapping in <c>helpers/xml.ts</c> (no JSON handling), a declared XML error
    /// model, and an <c>application/problem+xml</c> error read into a ProblemDetails object.
    /// </param>
    /// <param name="hasModelOrFile">When true, an operation answers a model or a file: the <c>modelOrFile</c> request option, asked for as bytes, a model answer read back as text.</param>
    /// <param name="hasResponseHeaders">When true, an operation's result arms have typed headers: the <c>responseHeaders</c> request option and their readers.</param>
    /// <returns>The TypeScript file content.</returns>
    public static string Generate(
        string? headerContent,
        bool hasRetry = false,
        bool zodRuntimeValidate = false,
        bool hasWireNames = false,
        bool convertDates = false,
        bool hasXml = false,
        bool hasModelOrFile = false,
        bool hasResponseHeaders = false,
        bool hasSequenceBodies = false)
    {
        var sb = new StringBuilder();

        sb.Append(headerContent);

        AppendImports(sb, hasRetry, zodRuntimeValidate, hasWireNames, hasXml);
        if (convertDates)
        {
            TypeScriptOperationHelper.AppendDateOnlyHelper(sb);
        }

        AppendUnparseableBodyClass(sb);
        AppendApiClientOptionsInterface(sb, hasRetry);
        TypeScriptApiClientRuntime.AppendRequestOptionsInterface(sb, hasRetry, zodRuntimeValidate, hasWireNames, hasXml, hasModelOrFile, hasResponseHeaders, hasSequenceBodies);
        TypeScriptOperationHelper.AppendParseEventStreamHelper(sb);
        if (hasResponseHeaders)
        {
            TypeScriptResponseHeaders.AppendRuntime(sb, axios: true);
        }

        if (hasSequenceBodies)
        {
            // Axios sends a body of records in one piece
            TypeScriptRequestRecords.AppendRuntime(sb, streams: false);
        }

        AppendApiClientClass(sb, hasRetry, zodRuntimeValidate, hasWireNames, hasXml, hasModelOrFile, hasResponseHeaders, hasSequenceBodies);

        return sb.ToString();
    }

    private static void AppendImports(
        StringBuilder sb,
        bool hasRetry,
        bool zodRuntimeValidate,
        bool hasWireNames,
        bool hasXml)
    {
        sb.AppendLine("import axios from 'axios';");
        sb.AppendLine("import type { AxiosInstance, AxiosRequestConfig, AxiosResponse, InternalAxiosRequestConfig } from 'axios';");
        sb.AppendLine("import { ApiError, decodeText, errorMessage } from '../errors/ApiError';");
        sb.AppendLine("import { ValidationError } from '../errors/ValidationError';");
        if (hasXml)
        {
            sb.AppendLine("import { isXmlMediaType, parseXml, readProblemXml, xmlSuccessArm, type XmlOptions } from '../helpers/xml';");
        }

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
        TypeScriptSecurity.AppendSecuritySchemeType(sb);
        sb.AppendLine("export interface ApiClientOptions {");
        sb.AppendLine("  /** The access token, sent as a bearer token with the operations that require one (http bearer, OAuth2, OpenID Connect). */");
        sb.AppendLine("  getAccessToken?: () => string | Promise<string>;");
        TypeScriptSecurity.AppendApiKeysOption(sb);
        sb.AppendLine("  defaultHeaders?: Record<string, string>;");
        sb.AppendLine("  requestInterceptors?: AxiosRequestInterceptor[];");
        sb.AppendLine("  responseInterceptors?: AxiosResponseInterceptor[];");
        sb.AppendLine("  /**");
        sb.AppendLine("   * The axios instance every request is sent with - your own interceptors, adapter, agent or defaults; defaults to a");
        sb.AppendLine("   * new instance. The client's own settings (base URL, status handling, body parsing, query format) go with each");
        sb.AppendLine("   * request, so the instance's defaults for those do not apply; defaultHeaders go with each request too, and");
        sb.AppendLine("   * requestInterceptors / responseInterceptors are added to the instance.");
        sb.AppendLine("   */");
        sb.AppendLine("  axiosInstance?: AxiosInstance;");

        if (hasRetry)
        {
            sb.AppendLine("  /** Retry policy for failed requests. Set to false to disable retry. Defaults to the spec-defined policy. */");
            sb.AppendLine("  retryPolicy?: RetryPolicy | false;");
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendApiClientClass(
        StringBuilder sb,
        bool hasRetry,
        bool zodRuntimeValidate,
        bool hasWireNames,
        bool hasXml,
        bool hasModelOrFile,
        bool hasResponseHeaders,
        bool hasSequenceBodies)
    {
        TypeScriptResultArms.AppendFitToArms(sb);
        sb.AppendLine("/** A request's config with what the operation reads: every body is asked for as bytes (responseType), and bodyType says what to make of them. */");
        sb.AppendLine("type BodyRequestConfig = AxiosRequestConfig & { bodyType?: 'json' | 'text' | 'arraybuffer' | 'auto' };");
        sb.AppendLine();
        sb.AppendLine("/** What the operation reads: its bodyType ('auto': whatever the Content-Type says), or - for a request made without one - its responseType. */");
        sb.AppendLine("function bodyTypeOf(config: unknown): string | undefined {");
        sb.AppendLine("  const request = config as BodyRequestConfig | undefined;");
        sb.AppendLine("  return request?.bodyType ?? request?.responseType;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("export class ApiClient {");
        sb.AppendLine("  private readonly baseUrl: string;");
        sb.AppendLine("  private readonly client: AxiosInstance;");
        sb.AppendLine("  private readonly options: ApiClientOptions;");
        sb.AppendLine("  // What every request carries: the client's settings, and the default headers when the instance is the caller's");
        sb.AppendLine("  private readonly transport: AxiosRequestConfig;");
        sb.AppendLine("  private readonly requestHeaders: Record<string, string>;");

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
            TypeScriptApiClientRuntime.AppendSetStrictModeMethod(sb);
        }

        AppendRequestMethod(sb, hasRetry, zodRuntimeValidate, hasWireNames, hasXml, hasModelOrFile, hasResponseHeaders, hasSequenceBodies);
        if (hasWireNames)
        {
            TypeScriptOperationHelper.AppendRenamingRequestStream(sb);
        }

        AppendRequestStreamMethod(sb, hasWireNames, hasSequenceBodies);
        TypeScriptSecurity.AppendCredentialsMethod(sb);
        AppendHandleResponseMethod(sb, zodRuntimeValidate, hasWireNames, hasXml);

        sb.AppendLine("}");
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

        sb.AppendLine("    this.transport = {");
        sb.AppendLine("      baseURL: this.baseUrl,");
        sb.AppendLine("      validateStatus: () => true,");

        // Suppress axios 1.x bracket notation (tags[]=a) and emit repeated keys (tags=a&tags=b)
        // so ASP.NET Core's default model binder can bind array query params correctly.
        sb.AppendLine("      paramsSerializer: { indexes: null },");

        // transformResponse is always emitted: it is the single place
        // where a JSON parse failure is observable, so it must own the parse. It is a function (not
        // an arrow) so `this` is the request config: responseType 'json' (a JSON operation) parses the
        // body of a success whatever the content-type says, and responseType 'text' returns it verbatim
        // even for a JSON content-type. An error body, and every body otherwise, is gated on the
        // content-type: only JSON bodies (application/json and +json suffixes such as
        // application/problem+json) are parsed, and only they can yield the UnparseableBody sentinel.
        // Text bodies (text/plain, application/xml, a proxy's text/html page) are returned verbatim so
        // they never regress into a parse error and a foreign error body is not lost.
        const string jsonParse = "JSON.parse(data)";

        sb.AppendLine("      transformResponse: [function (this: InternalAxiosRequestConfig, data: unknown, headers?: Record<string, string>, status?: number) {");
        sb.AppendLine("        const bodyType = bodyTypeOf(this);");
        sb.AppendLine("        const rawContentType = String(headers?.['content-type'] ?? headers?.['Content-Type'] ?? '');");
        sb.AppendLine("        // Every body arrives as bytes: its text is decoded by the charset the Content-Type names, which the");
        sb.AppendLine("        // adapters do not agree on (Node's http adapter and the fetch adapter read every text as UTF-8)");
        sb.AppendLine("        if ((this as BodyRequestConfig).bodyType !== 'arraybuffer' && (Object.prototype.toString.call(data) === '[object ArrayBuffer]' || ArrayBuffer.isView(data))) {");
        sb.AppendLine("          data = decodeText(data as ArrayBuffer, rawContentType);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (typeof data !== 'string' || data.length === 0) return data;");
        sb.AppendLine("        if (bodyType === 'text') return data;");
        sb.AppendLine("        const contentType = rawContentType.toLowerCase();");
        sb.AppendLine("        const successReadAsJson = bodyType === 'json' && (status ?? 200) < 400;");
        sb.AppendLine("        if (!successReadAsJson && !contentType.includes('application/json') && !contentType.includes('+json')) return data;");
        sb.AppendLine("        try {");
        sb.Append("          return ").Append(jsonParse).AppendLine(";");
        sb.AppendLine("        } catch {");
        sb.AppendLine("          return new UnparseableBody(data);");
        sb.AppendLine("        }");
        sb.AppendLine("      }],");

        sb.AppendLine("    };");
        sb.AppendLine("    this.client = this.options.axiosInstance ?? axios.create(this.transport);");
        sb.AppendLine("    this.requestHeaders = this.options.axiosInstance ? { ...this.options.defaultHeaders } : {};");
        sb.AppendLine();
        sb.AppendLine("    if (this.options.defaultHeaders && !this.options.axiosInstance) {");
        sb.AppendLine("      for (const [key, value] of Object.entries(this.options.defaultHeaders)) {");
        sb.AppendLine("        this.client.defaults.headers.common[key] = value;");
        sb.AppendLine("      }");
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
        bool hasWireNames,
        bool hasXml,
        bool hasModelOrFile,
        bool hasResponseHeaders,
        bool hasSequenceBodies)
    {
        var jsonBody = hasWireNames ? "toWire(options.body, options.wire?.body)" : "options.body";
        sb.AppendLine("  async request<T>(method: string, path: string, options?: RequestOptions): Promise<ApiResult<T>> {");
        sb.AppendLine("    let data: unknown;");
        sb.AppendLine("    const headers: Record<string, string> = { ...this.requestHeaders };");
        sb.AppendLine();
        sb.AppendLine("    if (options?.body !== undefined) {");
        if (hasSequenceBodies)
        {
            // A body of records, sent in one piece
            sb.AppendLine("      if (options.sequence) {");
            TypeScriptRequestRecords.AppendAxiosRecordsBody(sb, hasWireNames, "        ");
            sb.AppendLine("      } else if (options.body instanceof FormData) {");
        }
        else
        {
            sb.AppendLine("      if (options.body instanceof FormData) {");
        }

        sb.AppendLine("        // Named, not left to the adapter: axios's fetch adapter in Node labels a FormData form-urlencoded.");
        sb.AppendLine("        // Axios adds the boundary; in a browser it drops the header and the browser sets it.");
        sb.AppendLine("        headers['Content-Type'] = 'multipart/form-data';");
        sb.AppendLine("        data = options.body;");
        sb.AppendLine("      } else if (options.body instanceof URLSearchParams) {");
        sb.AppendLine("        headers['Content-Type'] = 'application/x-www-form-urlencoded';");
        sb.AppendLine("        data = options.body;");
        if (hasXml)
        {
            sb.AppendLine("      } else if (options.xml?.write) {");
            sb.AppendLine("        headers['Content-Type'] = options.xml.contentType ?? 'application/xml; charset=utf-8';");
            sb.AppendLine("        data = options.xml.write(options.body as never);");
        }

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

        // The credentials the operation's security schemes require - set on the request, not by an interceptor, so the
        // request interceptors see them - then the operation's headers.
        AppendCredentials(sb);

        // Skip undefined values — the generated client method passes optional header
        // params verbatim, and axios should not see literal undefined in the headers map.
        sb.AppendLine("    if (options?.headers) {");
        sb.AppendLine("      for (const [key, value] of Object.entries(options.headers)) {");
        sb.AppendLine("        if (value !== undefined) {");
        sb.AppendLine("          headers[key] = String(value);");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        if (hasXml)
        {
            sb.AppendLine();
            sb.AppendLine("    if (options?.xml?.accept) {");
            sb.AppendLine("      headers['Accept'] = options.xml.accept;");
            sb.AppendLine("    }");
        }

        sb.AppendLine();

        if (hasRetry)
        {
            sb.AppendLine("    // doRequest accepts an optional override signal so the retry path can hand each");
            sb.AppendLine("    // attempt a fresh AbortSignal — that way policy.timeoutMs actually cancels axios.");
            sb.AppendLine("    const doRequest = (attemptSignal?: AbortSignal) => this.client.request<T>({");
            sb.AppendLine("      ...this.transport,");
            sb.AppendLine("      method,");
            sb.AppendLine("      url: path,");
            sb.AppendLine("      data,");
            sb.AppendLine("      params,");
            sb.AppendLine("      headers,");
            sb.AppendLine("      signal: attemptSignal ?? options?.signal,");
            sb.AppendLine(TypeScriptModelOrFileExtractor.AxiosResponseType(hasModelOrFile));
            sb.AppendLine("    } as BodyRequestConfig);");
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
            sb.AppendLine("      ...this.transport,");
            sb.AppendLine("      method,");
            sb.AppendLine("      url: path,");
            sb.AppendLine("      data,");
            sb.AppendLine("      params,");
            sb.AppendLine("      headers,");
            sb.AppendLine("      signal: options?.signal,");
            sb.AppendLine(TypeScriptModelOrFileExtractor.AxiosResponseType(hasModelOrFile));
            sb.AppendLine("    } as BodyRequestConfig);");
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
        sb.AppendLine("        : decodeText(bytes, fileType);");
        sb.AppendLine("    }");
        sb.AppendLine();

        TypeScriptModelOrFileExtractor.AppendAxiosModelBodyRead(sb, hasModelOrFile);
        var wireArgument = hasWireNames ? ", options?.wire" : string.Empty;
        var xmlArgument = hasXml ? ", options?.xml" : string.Empty;
        var schemaArgument = zodRuntimeValidate ? ", options?.parseSchema" : string.Empty;
        sb.Append("    return ")
            .Append(TypeScriptResponseHeaders.Returned($"fitToArms(await this.handleResponse<T>(response{schemaArgument}{wireArgument}{xmlArgument}), options?.arms)", hasResponseHeaders))
            .AppendLine(";");

        sb.AppendLine("  }");
        sb.AppendLine();
    }

    /// <summary>
    /// Emits the credentials of the operation's security schemes into the <c>headers</c> record and the <c>params</c>
    /// (the query, with an API key that goes in the query).
    /// </summary>
    private static void AppendCredentials(StringBuilder sb)
    {
        sb.AppendLine("    for (const [key, value] of Object.entries(await this.credentials(options?.security, 'headers'))) {");
        sb.AppendLine("      headers[key] = value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const params = { ...options?.query, ...(await this.credentials(options?.security, 'query')) };");
        sb.AppendLine();
    }

    /// <summary>
    /// A declared XML error model is read into the model; an XML success body is read by the operation's mapping, with
    /// no JSON handling (transformResponse leaves a non-JSON body as its text). First in <c>handleResponse</c>, so it
    /// reads the body as the request left it (a model-or-file answer's model already back as text).
    /// </summary>
    private static void AppendReadXmlResponse(StringBuilder sb)
    {
        sb.AppendLine("    // An XML body: read by the operation's mapping (no JSON handling)");
        sb.AppendLine("    const responseContentType = String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? '');");
        sb.AppendLine("    const readXmlError = xml?.errors?.[String(response.status)] ?? xml?.errors?.['default'];");
        sb.AppendLine("    if (readXmlError && (response.status < 200 || response.status >= 300) && isXmlMediaType(responseContentType) && typeof response.data === 'string') {");
        sb.AppendLine("      try {");
        sb.AppendLine("        (response as { data: unknown }).data = readXmlError(parseXml(response.data));");
        sb.AppendLine("      } catch {");
        sb.AppendLine("        // Not the declared model: the error arm gets the text");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (xml?.read && response.status >= 200 && response.status < 300 && response.status !== 204 && response.status !== 205 && isXmlMediaType(responseContentType) && typeof response.data === 'string') {");
        sb.AppendLine("      try {");
        sb.AppendLine("        return { status: xmlSuccessArm(response.status), data: xml.read(parseXml(response.data)) as T, response } as ApiResult<T>;");
        sb.AppendLine("      } catch (parseError) {");
        sb.AppendLine("        return { status: 'parseError', error: parseError as Error, response };");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void AppendRequestStreamMethod(
        StringBuilder sb,
        bool hasWireNames,
        bool hasSequenceBodies)
    {
        const string multipartParse = "JSON.parse(bodyText)";

        sb.AppendLine("  // A stream goes through the axios instance like every request - the default headers, the access token and the");
        sb.AppendLine("  // request and response interceptors apply - with axios' fetch adapter, whose 'stream' response is the body's");
        sb.AppendLine("  // ReadableStream (axios 1.7 or later).");

        // With renaming, requestStream renames each item this reader yields.
        sb.AppendLine(hasWireNames
            ? "  private async *readStream<T>(method: string, path: string, options?: RequestOptions, framing: StreamFraming = 'json-array', sseItemEvents?: readonly string[]): AsyncGenerator<T> {"
            : "  async *requestStream<T>(method: string, path: string, options?: RequestOptions, framing: StreamFraming = 'json-array', sseItemEvents?: readonly string[]): AsyncGenerator<T> {");
        sb.AppendLine("    const headers: Record<string, string> = { ...this.requestHeaders };");
        AppendCredentials(sb);
        sb.AppendLine("    if (options?.headers) {");
        sb.AppendLine("      for (const [key, value] of Object.entries(options.headers)) {");
        sb.AppendLine("        if (value !== undefined) {");
        sb.AppendLine("          headers[key] = String(value);");
        sb.AppendLine("        }");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // A streamed search (POST or QUERY) sends its criteria as JSON, renamed to the API's property names
        sb.AppendLine("    let data: unknown;");
        if (hasSequenceBodies)
        {
            // A body of records streamed in while the answer streams out: sent in one piece
            sb.AppendLine("    if (options?.body !== undefined && options.sequence) {");
            TypeScriptRequestRecords.AppendAxiosRecordsBody(sb, hasWireNames, "      ");
            sb.AppendLine("    } else if (options?.body !== undefined) {");
        }
        else
        {
            sb.AppendLine("    if (options?.body !== undefined) {");
        }

        sb.AppendLine("      headers['Content-Type'] = 'application/json';");
        sb.Append("      data = ").Append(hasWireNames ? "toWire(options.body, options.wire?.body)" : "options.body").AppendLine(";");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const response = await this.client.request<ReadableStream<Uint8Array> | undefined>({");
        sb.AppendLine("      ...this.transport,");
        sb.AppendLine("      method,");
        sb.AppendLine("      url: path,");
        sb.AppendLine("      data,");
        sb.AppendLine("      params,");
        sb.AppendLine("      headers,");
        sb.AppendLine("      signal: options?.signal,");
        sb.AppendLine("      adapter: 'fetch',");
        sb.AppendLine("      responseType: 'stream',");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    if (response.status < 200 || response.status >= 300) {");
        sb.AppendLine("      // The error body is read like any error body: as text, then a ProblemDetails / model / JSON string, or text");
        sb.AppendLine("      const text = response.data ? decodeText(await new Response(response.data).arrayBuffer(), String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? '')) : '';");
        sb.AppendLine("      const result = await this.handleResponse<T>({ ...response, data: text, config: { ...response.config, responseType: 'text' } } as unknown as AxiosResponse<T>);");
        sb.AppendLine("      if ('error' in result) {");
        sb.AppendLine("        throw result.error;");
        sb.AppendLine("      }");
        sb.AppendLine();
        sb.AppendLine("      throw new ApiError(response.status, response.statusText, 'Stream request failed', response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const reader = response.data?.getReader();");
        sb.AppendLine("    if (!reader) {");
        sb.AppendLine("      throw new ApiError(0, 'NoBody', 'Response body is empty', response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const decoder = new TextDecoder();");
        sb.AppendLine();
        sb.AppendLine("    if (framing === 'sse') {");
        sb.AppendLine("      yield* parseEventStream<T>(reader, decoder, undefined, sseItemEvents, options?.onEvent);");
        sb.AppendLine("      return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (framing === 'multipart') {");
        sb.AppendLine("      const contentType = String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? '');");
        TypeScriptApiClientRuntime.AppendMultipartFraming(sb, multipartParse);
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendHandleResponseMethod(
        StringBuilder sb,
        bool zodRuntimeValidate,
        bool hasWireNames,
        bool hasXml)
    {
        var wireParameter = hasWireNames ? ", wire?: WireShapes" : string.Empty;
        var xmlParameter = hasXml ? ", xml?: XmlOptions" : string.Empty;
        var signature = zodRuntimeValidate
            ? "  private async handleResponse<T>(response: AxiosResponse<T>, parseSchema?: ZodTypeAny" + wireParameter + xmlParameter + "): Promise<ApiResult<T>> {"
            : "  private async handleResponse<T>(response: AxiosResponse<T>" + wireParameter + xmlParameter + "): Promise<ApiResult<T>> {";
        sb.AppendLine(signature);
        if (hasXml)
        {
            AppendReadXmlResponse(sb);
        }

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
        sb.AppendLine("      if ((response.data as unknown) === '' && bodyTypeOf(response.config) !== 'text') {");
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
        sb.AppendLine("    let errorBodyIsRawText = bodyTypeOf(response.config) === 'text' || bodyTypeOf(response.config) === 'arraybuffer';");
        sb.AppendLine("    if (typeof Blob !== 'undefined' && errorBody instanceof Blob) {");
        sb.AppendLine("      // A download's error arrives as a Blob (responseType 'blob'): read it like any other error body.");
        sb.AppendLine("      errorBody = decodeText(await errorBody.arrayBuffer(), String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? ''));");
        sb.AppendLine("      errorBodyIsRawText = true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (typeof errorBody === 'string') {");
        sb.AppendLine("      const errorContentType = String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? '').toLowerCase();");
        sb.AppendLine("      if (errorBody.length === 0) {");
        sb.AppendLine("        errorBody = undefined;");
        if (hasXml)
        {
            // An application/problem+xml error (RFC 9457 Appendix B), read into a ProblemDetails object
            sb.AppendLine("      } else if (errorContentType.includes('application/problem+xml')) {");
            sb.AppendLine("        try {");
            sb.AppendLine("          errorBody = readProblemXml(parseXml(errorBody));");
            sb.AppendLine("        } catch {");
            sb.AppendLine("          errorBody = undefined;");
            sb.AppendLine("        }");
        }

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
        sb.AppendLine("    const message = errorMessage(response.status, response.statusText, errorBody, String(response.headers?.['content-type'] ?? response.headers?.['Content-Type'] ?? ''));");
        sb.AppendLine();

        TypeScriptApiClientRuntime.AppendErrorArms(sb, hasWireNames);
        sb.AppendLine("  }");
    }
}