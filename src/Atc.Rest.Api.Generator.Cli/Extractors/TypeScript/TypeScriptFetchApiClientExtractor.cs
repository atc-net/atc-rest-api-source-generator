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
    /// <param name="hasXml">
    /// When true, the specification has XML bodies: the <c>xml</c> request option (Accept, an XML request body), an XML
    /// success body read by the operation's mapping in <c>helpers/xml.ts</c> (before the JSON handling - no key
    /// renaming, no schema check), a declared XML error model, and an <c>application/problem+xml</c> error read into a
    /// ProblemDetails object.
    /// </param>
    /// <param name="hasModelOrFile">When true, an operation answers a model or a file: the <c>modelOrFile</c> request option.</param>
    /// <param name="hasResponseHeaders">When true, an operation's result arms have typed headers: the <c>responseHeaders</c> request option and their readers.</param>
    /// <param name="hasSequenceBodies">When true, an operation takes a request body of records (JSON Lines, JSON Text Sequences): the <c>sequence</c> request option, the <c>requestStreaming</c> client option and the record writers.</param>
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

        AppendApiClientOptionsInterface(sb, hasRetry, hasSequenceBodies);
        AppendRequestOptionsInterface(sb, hasRetry, zodRuntimeValidate, hasWireNames, hasXml, hasModelOrFile, hasResponseHeaders, hasSequenceBodies);
        TypeScriptOperationHelper.AppendParseEventStreamHelper(sb);
        if (hasResponseHeaders)
        {
            TypeScriptResponseHeaders.AppendRuntime(sb, axios: false);
        }

        if (hasSequenceBodies)
        {
            TypeScriptRequestRecords.AppendRuntime(sb, streams: true);
        }

        AppendApiClientClass(sb, hasRetry, zodRuntimeValidate, hasWireNames, hasXml, hasResponseHeaders, hasSequenceBodies);

        return sb.ToString();
    }

    private static void AppendImports(
        StringBuilder sb,
        bool hasRetry,
        bool zodRuntimeValidate,
        bool hasWireNames,
        bool hasXml)
    {
        sb.AppendLine("import { ApiError, decodeText, errorMessage } from '../errors/ApiError';");
        sb.AppendLine("import { ValidationError } from '../errors/ValidationError';");
        if (hasXml)
        {
            sb.AppendLine("import { isXmlMediaType, parseXml, readProblemXml, withResponse, xmlSuccessArm, type XmlOptions } from '../helpers/xml';");
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
            // ZodTypeAny is the broad parent type — accepts any z.object / z.array /
            // primitive / discriminated union. Type-only import keeps the bundle clean
            // for consumers who set the flag but never narrow into schemaMismatch.
            sb.AppendLine("import type { ZodTypeAny } from 'zod';");
        }

        sb.AppendLine();
    }

    private static void AppendApiClientOptionsInterface(
        StringBuilder sb,
        bool hasRetry,
        bool hasSequenceBodies)
    {
        sb.AppendLine("export type FetchRequestInterceptor = (url: string, init: RequestInit) => RequestInit | Promise<RequestInit>;");
        sb.AppendLine("export type FetchResponseInterceptor = (response: Response) => Response | Promise<Response>;");
        sb.AppendLine();
        TypeScriptSecurity.AppendSecuritySchemeType(sb);
        sb.AppendLine("export interface ApiClientOptions {");
        sb.AppendLine("  /** The access token, sent as a bearer token with the operations that require one (http bearer, OAuth2, OpenID Connect). */");
        sb.AppendLine("  getAccessToken?: () => string | Promise<string>;");
        TypeScriptSecurity.AppendApiKeysOption(sb);
        sb.AppendLine("  defaultHeaders?: Record<string, string>;");
        sb.AppendLine("  requestInterceptors?: FetchRequestInterceptor[];");
        sb.AppendLine("  responseInterceptors?: FetchResponseInterceptor[];");
        sb.AppendLine("  /** The fetch every request is sent with - an SSR framework's own, or a test double; defaults to the global fetch. It is called as a plain function, so the browser's window.fetch works as it is. */");
        sb.AppendLine("  fetch?: typeof fetch;");

        if (hasRetry)
        {
            sb.AppendLine("  /** Retry policy for failed requests. Set to false to disable retry. Defaults to the spec-defined policy. */");
            sb.AppendLine("  retryPolicy?: RetryPolicy | false;");
        }

        if (hasSequenceBodies)
        {
            TypeScriptRequestRecords.AppendClientOption(sb);
        }

        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendRequestOptionsInterface(
        StringBuilder sb,
        bool hasRetry,
        bool zodRuntimeValidate,
        bool hasWireNames,
        bool hasXml,
        bool hasModelOrFile,
        bool hasResponseHeaders,
        bool hasSequenceBodies)
    {
        sb.AppendLine("export interface RequestOptions {");
        sb.AppendLine("  body?: unknown;");
        sb.AppendLine("  query?: Record<string, string | number | boolean | (string | number | boolean)[] | undefined>;");
        sb.AppendLine("  headers?: Record<string, string | number | boolean | undefined>;");
        TypeScriptSecurity.AppendSecurityRequestOption(sb);
        sb.AppendLine("  signal?: AbortSignal;");
        sb.AppendLine("  responseType?: 'json' | 'blob' | 'text';");
        sb.AppendLine("  /** The media type of a file sent as the raw body (the generated client passes the declared one); else the file's own type. */");
        sb.AppendLine("  contentType?: string;");
        sb.AppendLine("  /** The result arms of the operation's union (the generated client passes them): a status it does not declare is folded into one it does. */");
        sb.AppendLine("  arms?: readonly string[];");
        TypeScriptResponseHeaders.AppendRequestOption(sb, hasResponseHeaders);
        sb.AppendLine("  /** Called for every Server-Sent Event that is yielded as an item, before it is yielded: with the id to send as Last-Event-ID to continue the stream after it. */");
        sb.AppendLine("  onEvent?: (event: ServerSentEventInfo) => void;");
        TypeScriptModelOrFileExtractor.AppendRequestOption(sb, hasModelOrFile);
        TypeScriptOperationHelper.AppendXmlRequestOption(sb, hasXml);
        if (hasSequenceBodies)
        {
            TypeScriptRequestRecords.AppendRequestOption(sb);
        }

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
        sb.AppendLine("/**");
        sb.AppendLine(" * What a Server-Sent Event says besides its data: the id of the last event read (its own, or an earlier one's - an");
        sb.AppendLine(" * id stays until another is sent; empty when the server has reset it), the event's name (undefined for the unnamed");
        sb.AppendLine(" * event) and the time in milliseconds the server last asked a client to wait before it reconnects.");
        sb.AppendLine(" */");
        sb.AppendLine("export interface ServerSentEventInfo {");
        sb.AppendLine("  lastEventId?: string;");
        sb.AppendLine("  event?: string;");
        sb.AppendLine("  retry?: number;");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendApiClientClass(
        StringBuilder sb,
        bool hasRetry,
        bool zodRuntimeValidate,
        bool hasWireNames,
        bool hasXml,
        bool hasResponseHeaders,
        bool hasSequenceBodies)
    {
        TypeScriptResultArms.AppendFitToArms(sb);
        sb.AppendLine("/** A body as text, in the charset its Content-Type names (fetch's own text() reads every body as UTF-8). */");
        sb.AppendLine("async function readText(response: Response): Promise<string> {");
        sb.AppendLine("  return decodeText(await response.arrayBuffer(), response.headers.get('Content-Type'));");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("export class ApiClient {");
        sb.AppendLine("  private readonly baseUrl: string;");
        sb.AppendLine("  private readonly options: ApiClientOptions;");
        sb.AppendLine("  private readonly send: (url: string, init: RequestInit) => Promise<Response>;");

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

        AppendRequestMethod(sb, hasRetry, zodRuntimeValidate, hasWireNames, hasXml, hasResponseHeaders, hasSequenceBodies);
        if (hasSequenceBodies)
        {
            TypeScriptRequestRecords.AppendFetchRecordsBodyMethod(sb, hasWireNames);
        }

        if (hasWireNames)
        {
            TypeScriptOperationHelper.AppendRenamingRequestStream(sb);
        }

        AppendRequestStreamMethod(sb, hasWireNames, hasSequenceBodies);
        AppendBuildUrlMethod(sb);
        AppendGetHeadersMethod(sb);
        AppendHandleResponseMethod(sb, zodRuntimeValidate, hasWireNames, hasXml);

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
        sb.AppendLine("    const transport = this.options.fetch;");
        sb.AppendLine("    this.send = (url, init) => (transport ?? fetch)(url, init);");

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
        bool hasWireNames,
        bool hasXml,
        bool hasResponseHeaders,
        bool hasSequenceBodies)
    {
        var body = hasWireNames ? "toWire(options.body, options.wire?.body)" : "options.body";
        var stringify = $"JSON.stringify({body})";

        sb.AppendLine("  async request<T>(method: string, path: string, options?: RequestOptions): Promise<ApiResult<T>> {");
        sb.AppendLine("    const url = this.buildUrl(path, { ...options?.query, ...(await this.credentials(options?.security, 'query')) });");
        sb.AppendLine("    const headers = await this.getHeaders(options?.headers, options?.security);");
        if (hasXml)
        {
            sb.AppendLine("    if (options?.xml?.accept) {");
            sb.AppendLine("      headers.set('Accept', options.xml.accept);");
            sb.AppendLine("    }");
        }

        sb.AppendLine();
        sb.AppendLine("    let fetchBody: BodyInit | undefined;");
        if (hasSequenceBodies)
        {
            sb.AppendLine("    let streamed = false;");
        }

        sb.AppendLine("    if (options?.body !== undefined) {");
        if (hasSequenceBodies)
        {
            // A body of records: streamed as the source yields them, or buffered
            sb.AppendLine("      if (options.sequence) {");
            sb.AppendLine("        const records = await this.recordsBody(options, headers);");
            sb.AppendLine("        fetchBody = records.body;");
            sb.AppendLine("        streamed = records.streamed;");
            sb.AppendLine("      } else if (options.body instanceof FormData) {");
        }
        else
        {
            sb.AppendLine("      if (options.body instanceof FormData) {");
        }

        sb.AppendLine("        fetchBody = options.body;");
        sb.AppendLine("      } else if (options.body instanceof URLSearchParams) {");
        sb.AppendLine("        headers.set('Content-Type', 'application/x-www-form-urlencoded');");
        sb.AppendLine("        fetchBody = options.body;");
        sb.AppendLine("      } else if (options.body instanceof Blob) {");
        sb.AppendLine("        headers.set('Content-Type', options.contentType ?? (options.body.type || 'application/octet-stream'));");
        sb.AppendLine("        fetchBody = options.body;");
        if (hasXml)
        {
            sb.AppendLine("      } else if (options.xml?.write) {");
            sb.AppendLine("        headers.set('Content-Type', options.xml.contentType ?? 'application/xml; charset=utf-8');");
            sb.AppendLine("        fetchBody = options.xml.write(options.body as never);");
        }

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
        if (hasSequenceBodies)
        {
            AppendDuplex(sb);
        }

        sb.AppendLine();
        sb.AppendLine("    for (const interceptor of this.options.requestInterceptors ?? []) {");
        sb.AppendLine("      init = await interceptor(url, init);");
        sb.AppendLine("    }");
        sb.AppendLine();

        if (hasRetry)
        {
            sb.AppendLine("    const retryPolicy = resolveRetryPolicy(method, this.retryPolicy, options?.retry);");

            // A streamed body is read once, as it is sent: it is never retried
            sb.AppendLine(hasSequenceBodies ? "    if (retryPolicy && !streamed) {" : "    if (retryPolicy) {");
            sb.AppendLine("      // Each attempt gets its own signal, so policy.timeoutMs cancels the in-flight fetch and a parent abort");
            sb.AppendLine("      // reaches every attempt; the body is read inside the attempt, so both cover the body read too.");
            sb.AppendLine("      return retryWithBackoff(");
            sb.AppendLine("        (attemptSignal) => this.send(url, { ...init, signal: attemptSignal }),");
            sb.AppendLine("        retryPolicy,");
            sb.AppendLine("        options?.signal,");
            sb.AppendLine("        (response) => this.readResponse<T>(response, options),");
            sb.AppendLine("      );");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("    return this.readResponse<T>(await this.send(url, init), options);");
        sb.AppendLine("  }");
        sb.AppendLine();

        sb.AppendLine("  private async readResponse<T>(response: Response, options?: RequestOptions): Promise<ApiResult<T>> {");
        sb.AppendLine("    for (const interceptor of this.options.responseInterceptors ?? []) {");
        sb.AppendLine("      response = await interceptor(response);");
        sb.AppendLine("    }");
        sb.AppendLine();
        if (hasXml)
        {
            AppendReadXmlResponse(sb, hasResponseHeaders);
        }

        var wireArgument = hasWireNames ? ", options?.wire" : string.Empty;
        var handleArguments = zodRuntimeValidate
            ? "response, options?.responseType, options?.parseSchema" + wireArgument
            : "response, options?.responseType" + wireArgument;

        // With XML, the result keeps the server's response when a declared XML error model was read through a stand-in
        sb.Append("    return ")
            .Append(TypeScriptResponseHeaders.Returned(
                hasXml
                    ? $"fitToArms(withResponse(serverResponse, await this.handleResponse<T>({handleArguments})), options?.arms)"
                    : $"fitToArms(await this.handleResponse<T>({handleArguments}), options?.arms)",
                hasResponseHeaders))
            .AppendLine(";");
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    /// <summary>
    /// A declared XML error model is read into the model and handed on as a JSON error body (the result keeps the
    /// server's response); an XML success body is read by the operation's mapping, with no JSON handling.
    /// </summary>
    private static void AppendReadXmlResponse(
        StringBuilder sb,
        bool hasResponseHeaders)
    {
        sb.AppendLine("    // A declared XML error model: read into the model and handed on as a JSON error body - the result keeps the");
        sb.AppendLine("    // server's response (its headers say XML)");
        sb.AppendLine("    let serverResponse: Response | undefined;");
        sb.AppendLine("    const readXmlError = options?.xml?.errors?.[String(response.status)] ?? options?.xml?.errors?.['default'];");
        sb.AppendLine("    if (readXmlError && !response.ok && isXmlMediaType(response.headers.get('Content-Type'))) {");
        sb.AppendLine("      try {");
        sb.AppendLine("        const errorModel = readXmlError(parseXml(await readText(response.clone())));");
        sb.AppendLine("        const errorHeaders = new Headers(response.headers);");
        sb.AppendLine("        errorHeaders.set('Content-Type', 'application/json');");
        sb.AppendLine("        serverResponse = response;");
        sb.AppendLine("        response = new Response(JSON.stringify(errorModel), { status: response.status, statusText: response.statusText, headers: errorHeaders });");
        sb.AppendLine("      } catch {");
        sb.AppendLine("        // Not the declared model: the error arm gets the text");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // An XML body: read by the operation's mapping (no JSON handling)");
        sb.AppendLine("    if (options?.xml?.read && response.ok && response.status !== 204 && response.status !== 205 && isXmlMediaType(response.headers.get('Content-Type'))) {");
        sb.AppendLine("      try {");
        sb.Append("        return ").Append(TypeScriptResponseHeaders.Returned("fitToArms({ status: xmlSuccessArm(response.status), data: options.xml.read(parseXml(await readText(response))) as T, response } as ApiResult<T>, options.arms)", hasResponseHeaders)).AppendLine(";");
        sb.AppendLine("      } catch (parseError) {");
        sb.Append("        return ").Append(TypeScriptResponseHeaders.Returned("fitToArms({ status: 'parseError', error: parseError as Error, response }, options.arms)", hasResponseHeaders)).AppendLine(";");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// A streamed request body needs fetch's <c>duplex: 'half'</c> (the DOM typings do not name it yet).
    /// </summary>
    private static void AppendDuplex(StringBuilder sb)
    {
        sb.AppendLine("    if (streamed) {");
        sb.AppendLine("      (init as RequestInit & { duplex: 'half' }).duplex = 'half';");
        sb.AppendLine("    }");
    }

    private static void AppendRequestStreamMethod(
        StringBuilder sb,
        bool hasWireNames,
        bool hasSequenceBodies)
    {
        const string multipartParse = "JSON.parse(bodyText)";

        // With renaming, requestStream (above) renames each item this reader yields.
        sb.AppendLine(hasWireNames
            ? "  private async *readStream<T>(method: string, path: string, options?: RequestOptions, framing: StreamFraming = 'json-array', sseItemEvents?: readonly string[]): AsyncGenerator<T> {"
            : "  async *requestStream<T>(method: string, path: string, options?: RequestOptions, framing: StreamFraming = 'json-array', sseItemEvents?: readonly string[]): AsyncGenerator<T> {");
        sb.AppendLine("    const url = this.buildUrl(path, { ...options?.query, ...(await this.credentials(options?.security, 'query')) });");
        sb.AppendLine("    const headers = await this.getHeaders(options?.headers, options?.security);");
        sb.AppendLine();
        TypeScriptOperationHelper.AppendStreamRequestBody(sb, hasWireNames, hasSequenceBodies);
        sb.AppendLine("    let init: RequestInit = {");
        sb.AppendLine("      method,");
        sb.AppendLine("      headers,");
        sb.AppendLine("      body: streamBody,");
        sb.AppendLine("      signal: options?.signal,");
        sb.AppendLine("    };");
        if (hasSequenceBodies)
        {
            AppendDuplex(sb);
        }

        sb.AppendLine();
        sb.AppendLine("    for (const interceptor of this.options.requestInterceptors ?? []) {");
        sb.AppendLine("      init = await interceptor(url, init);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // The same pipeline as any request: the response interceptors see the stream's response (a 401 refresh applies)");
        sb.AppendLine("    let response = await this.send(url, init);");
        sb.AppendLine("    for (const interceptor of this.options.responseInterceptors ?? []) {");
        sb.AppendLine("      response = await interceptor(response);");
        sb.AppendLine("    }");
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
        sb.AppendLine("      yield* parseEventStream<T>(reader, decoder, undefined, sseItemEvents, options?.onEvent);");
        sb.AppendLine("      return;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (framing === 'multipart') {");
        sb.AppendLine("      const contentType = response.headers.get('content-type') ?? '';");
        TypeScriptApiClientRuntime.AppendMultipartFraming(sb, multipartParse);
        sb.AppendLine("  }");
        sb.AppendLine();
    }

    private static void AppendBuildUrlMethod(StringBuilder sb)
        => TypeScriptOperationHelper.AppendBuildUrlMethod(sb, modifier: string.Empty);

    private static void AppendGetHeadersMethod(StringBuilder sb)
    {
        // `extra` values may be undefined (the generated client passes through optional
        // header params verbatim), so skip those entries — Headers.set rejects undefined.
        // Non-string scalars (number / boolean) are coerced to string to match how
        // headers travel on the wire.
        sb.AppendLine("  async getHeaders(extra?: Record<string, string | number | boolean | undefined>, security?: readonly SecurityScheme[]): Promise<Headers> {");
        sb.AppendLine("    const headers = new Headers(this.options.defaultHeaders);");
        sb.AppendLine();
        sb.AppendLine("    for (const [key, value] of Object.entries(await this.credentials(security, 'headers'))) {");
        sb.AppendLine("      headers.set(key, value);");
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
        TypeScriptSecurity.AppendCredentialsMethod(sb);
    }

    private static void AppendHandleResponseMethod(
        StringBuilder sb,
        bool zodRuntimeValidate,
        bool hasWireNames,
        bool hasXml)
    {
        const string parseJson = "JSON.parse(await readText(response))";

        var wireParameter = hasWireNames ? ", wire?: WireShapes" : string.Empty;
        var signature = zodRuntimeValidate
            ? "  private async handleResponse<T>(response: Response, responseType?: 'json' | 'blob' | 'text', parseSchema?: ZodTypeAny" + wireParameter + "): Promise<ApiResult<T>> {"
            : "  private async handleResponse<T>(response: Response, responseType?: 'json' | 'blob' | 'text'" + wireParameter + "): Promise<ApiResult<T>> {";
        sb.AppendLine(signature);
        sb.AppendLine("    // 204 and 205 never carry a body; 304 neither (RFC 9110). The empty body is still read to its end: a");
        sb.AppendLine("    // browser holds the response open until then, and one left unread is reported as a cancelled request.");
        sb.AppendLine("    if (response.status === 204 || response.status === 205) {");
        sb.AppendLine("      await response.arrayBuffer().catch(() => undefined);");
        sb.AppendLine("      return { status: 'noContent', response };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (response.status === 304) {");
        sb.AppendLine("      await response.arrayBuffer().catch(() => undefined);");
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
        sb.AppendLine("          data = await readText(response);");
        sb.AppendLine("        } else if (isJson) {");
        sb.AppendLine("          const text = await readText(response);");
        sb.AppendLine("          data = text.length === 0 ? undefined : JSON.parse(text);");
        sb.AppendLine("        } else if (responseType === 'blob' || contentType.length > 0) {");
        sb.AppendLine("          data = await response.blob();");
        sb.AppendLine("        } else {");
        sb.AppendLine("          const text = await readText(response);");
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
        sb.AppendLine("          const text = await readText(response);");
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
        if (hasXml)
        {
            // An application/problem+xml error (RFC 9457 Appendix B), read into a ProblemDetails object
            sb.AppendLine("      if (contentType.includes('application/problem+xml')) {");
            sb.AppendLine("        const text = await readText(response);");
            sb.AppendLine("        errorBody = text.length > 0 ? readProblemXml(parseXml(text)) : undefined;");
            sb.AppendLine("      } else if (contentType.includes('application/json') || contentType.includes('+json')) {");
        }
        else
        {
            sb.AppendLine("      if (contentType.includes('application/json') || contentType.includes('+json')) {");
        }

        sb.Append("        errorBody = ").Append(parseJson).AppendLine(";");
        sb.AppendLine("      } else if (contentType.startsWith('text/') || contentType.includes('application/xml') || contentType.includes('+xml')) {");
        sb.AppendLine("        const text = await readText(response);");
        sb.AppendLine("        errorBody = text.length > 0 ? text : undefined;");
        sb.AppendLine("      }");
        sb.AppendLine("    } catch {");
        sb.AppendLine("      errorBody = undefined;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    const message = errorMessage(response.status, response.statusText, errorBody, contentType);");
        sb.AppendLine();

        TypeScriptApiClientRuntime.AppendErrorArms(sb, hasWireNames);
        sb.AppendLine("  }");
    }
}