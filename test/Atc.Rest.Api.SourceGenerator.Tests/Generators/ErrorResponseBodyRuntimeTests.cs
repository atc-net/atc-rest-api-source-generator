namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Runtime characterization of error response bodies: what the generated server actually writes on
/// the wire for each way a spec can declare a 4xx/5xx response, and what each generated client
/// (every <c>errorResponseFormat</c>, plus both typed-client result styles) makes of those bytes.
/// </summary>
/// <remarks>
/// The spec, the markers and the handlers all live in this file, so no scenario snapshot folder is
/// involved. The handlers are derived from the factory methods the server generator actually emits,
/// which keeps the harness working when those signatures change.
/// </remarks>
public partial class ErrorResponseBodyRuntimeTests
{
    private const string ProjectName = "ErrorBodies";
    private const string ValidReason = "ok";
    private const string TooLongReason = "far-too-long";

    /// <summary>
    /// Where the redirect statuses point: the 200 operation of the status set.
    /// </summary>
    internal const string RedirectTarget = "/statuses/200/s200";

    /// <summary>
    /// Two operations with only a success and a <c>default</c> response: a problem-shaped default
    /// (<c>application/problem+json</c>) and a plain JSON one. The <c>{code}</c> path parameter makes the
    /// generator add 400 and 404 besides 500.
    /// </summary>
    private const string DefaultOnlyYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /problem-default/{code}:
            get:
              operationId: getProblemDefault
              parameters:
                - name: code
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
                default:
                  description: Error
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/ApiProblem'
          /json-default/{code}:
            get:
              operationId: getJsonDefault
              parameters:
                - name: code
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
                default:
                  description: Error
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/ErrorResponse'
          /pd-default/{code}:
            get:
              operationId: getPdDefault
              parameters:
                - name: code
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
                default:
                  description: Error
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/ProblemDetails'
        components:
          schemas:
            ProblemDetails:
              type: object
              properties:
                type:
                  type: string
                title:
                  type: string
                status:
                  type: integer
                detail:
                  type: string
                instance:
                  type: string
            Item:
              type: object
              properties:
                name:
                  type: string
            ApiProblem:
              type: object
              properties:
                type:
                  type: string
                title:
                  type: string
                status:
                  type: integer
                detail:
                  type: string
                code:
                  type: string
            ErrorResponse:
              type: object
              properties:
                code:
                  type: string
                message:
                  type: string
        """;

    /// <summary>
    /// Calls both <see cref="DefaultOnlyYaml"/> operations against a stub server that answers
    /// <c>/problem-default/{code}</c> with <c>application/problem+json</c> and <c>/json-default/{code}</c> with
    /// <c>application/json</c>, both with a body that fits every model involved; one row per true <c>Is…</c>
    /// property: <c>{operation}/{code}: {status} {IsX} {XContent} => {value}</c>.
    /// </summary>
    private const string DefaultOnlyProbeSource = """"
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated;
        using ErrorBodies.Generated.ProblemDefault.Client;
        using ErrorBodies.Generated.ProblemDefault.Endpoints.Interfaces;
        using ErrorBodies.Generated.JsonDefault.Client;
        using ErrorBodies.Generated.JsonDefault.Endpoints.Interfaces;
        using ErrorBodies.Generated.PdDefault.Client;
        using ErrorBodies.Generated.PdDefault.Endpoints.Interfaces;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
            public static async Task<List<string>> RunAsync()
            {
                var services = new ServiceCollection();
                services
                    .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                services.AddErrorBodiesEndpoints();
                using var provider = services.BuildServiceProvider();

                var rows = new List<string>();
                foreach (var code in new[] { "400", "404", "418", "500", "html" })
                {
                    Describe(rows, $"problemDefault/{code}", await provider.GetRequiredService<IGetProblemDefaultEndpoint>().ExecuteAsync(new GetProblemDefaultParameters(code)));
                    Describe(rows, $"jsonDefault/{code}", await provider.GetRequiredService<IGetJsonDefaultEndpoint>().ExecuteAsync(new GetJsonDefaultParameters(code)));
                    Describe(rows, $"pdDefault/{code}", await provider.GetRequiredService<IGetPdDefaultEndpoint>().ExecuteAsync(new GetPdDefaultParameters(code)));
                }

                return rows;
            }

            private static void Describe(List<string> rows, string name, EndpointResponse response)
            {
                var type = response.GetType();
                foreach (var isProperty in type.GetProperties().Where(p => p.PropertyType == typeof(bool) && p.Name.StartsWith("Is", StringComparison.Ordinal) && p.Name != "IsSuccess"))
                {
                    if (!(bool)isProperty.GetValue(response)!)
                    {
                        continue;
                    }

                    var contentProperty = type.GetProperty(isProperty.Name[2..] + "Content");
                    string value;
                    try
                    {
                        var content = contentProperty?.GetValue(response);
                        value = content is null ? "null" : $"{content.GetType().Name} {System.Text.Json.JsonSerializer.Serialize(content, content.GetType())}";
                    }
                    catch (System.Reflection.TargetInvocationException ex)
                    {
                        value = $"THROWS {ex.InnerException!.GetType().Name}: {ex.InnerException.Message}";
                    }

                    rows.Add($"{name}: {(int)response.StatusCode} {isProperty.Name} {contentProperty?.Name ?? "(no content property)"} => {value}");
                }
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var segments = request.RequestUri!.AbsolutePath.Trim('/').Split('/');
                    if (segments[1] == "html")
                    {
                        // A proxy's error page: neither JSON nor the API's error body.
                        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
                        {
                            Content = new StringContent("<html><body>502 Bad Gateway</body></html>", System.Text.Encoding.UTF8, "text/html"),
                        });
                    }

                    var code = int.Parse(segments[1], System.Globalization.CultureInfo.InvariantCulture);
                    var mediaType = segments[0] == "json-default" ? "application/json" : "application/problem+json";
                    var body = $$$"""{"type":"about:blank","title":"t{{{code}}}","status":{{{code}}},"detail":"{{{code}}} detail","errors":{"name":["name {{{code}}}"]},"code":"E{{{code}}}","message":"m{{{code}}}"}""";
                    return Task.FromResult(new HttpResponseMessage((System.Net.HttpStatusCode)code)
                    {
                        Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType),
                    });
                }
            }
        }
        """";

    private const string JsonStringContent = """
                  content:
                    application/json:
                      schema:
                        type: string
        """;

    private const string JsonProblemContent = """
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/ApiProblem'
        """;

    private const string ProblemJsonContent = """
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/ApiProblem'
        """;

    private const string FrameworkProblemJsonContent = """
                  content:
                    application/problem+json:
                      schema:
                        $ref: '#/components/schemas/ProblemDetails'
        """;

    private const string InlineObjectContent = """
                  content:
                    application/json:
                      schema:
                        type: object
                        properties:
                          code:
                            type: string
                          message:
                            type: string
        """;

    private const string TextPlainContent = """
                  content:
                    text/plain:
                      schema:
                        type: string
        """;

    private const string JsonCustomContent = """
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/ErrorResponse'
        """;

    internal const string ServerMarker = """
        {
          "useMinimalApiPackage": "Enabled",
          "useValidationFilter": "Enabled",
          "useGlobalErrorHandler": "Enabled"
        }
        """;

    /// <summary>
    /// The PUT operations. Each declares 204 plus 400/404/409/422, differing only in how the error
    /// responses declare their body.
    /// </summary>
    private static readonly SetOperation[] SetOperations =
    [
        new("no-content", "setNoContent", "NoContent", ErrorContent: null, IncludeConflict: true),
        new("mixed", "setMixed", "Mixed", ErrorContent: null, IncludeConflict: false),
        new("json-string", "setJsonString", "JsonString", JsonStringContent, IncludeConflict: true),
        new("json-problem", "setJsonProblem", "JsonProblem", JsonProblemContent, IncludeConflict: true),
        new("problem-json", "setProblemJson", "ProblemJson", ProblemJsonContent, IncludeConflict: true),
        new("text-plain", "setTextPlain", "TextPlain", TextPlainContent, IncludeConflict: true),
        new("json-custom", "setJsonCustom", "JsonCustom", JsonCustomContent, IncludeConflict: true),
        new("ref-response", "setRefResponse", "RefResponse", ErrorContent: null, IncludeConflict: false),
        new("inline-error", "setInlineError", "InlineError", InlineObjectContent, IncludeConflict: true),
        new("all-none", "setAllNone", "AllNone", ErrorContent: null, IncludeConflict: true, AllStatuses: true),
        new("all-json-problem", "setAllJsonProblem", "AllJsonProblem", JsonProblemContent, IncludeConflict: true, AllStatuses: true),
        new("all-framework-problem", "setAllFrameworkProblem", "AllFrameworkProblem", FrameworkProblemJsonContent, IncludeConflict: true, AllStatuses: true),
        new("all-json-string", "setAllJsonString", "AllJsonString", JsonStringContent, IncludeConflict: true, AllStatuses: true),
        new("all-text-plain", "setAllTextPlain", "AllTextPlain", TextPlainContent, IncludeConflict: true, AllStatuses: true),
    ];

    /// <summary>
    /// Every registered 4xx/5xx status code, plus two non-standard codes (499, 599). The <c>all-*</c>
    /// operations declare all of them.
    /// </summary>
    internal static readonly int[] AllErrorStatusCodes =
    [
        400, 401, 402, 403, 404, 405, 406, 407, 408, 409, 410, 411, 412, 413, 414, 415, 416, 417, 418,
        421, 422, 423, 424, 425, 426, 428, 429, 431, 451, 499,
        500, 501, 502, 503, 504, 505, 506, 507, 508, 510, 511, 599,
    ];

    /// <summary>
    /// Status codes that are not members of <c>System.Net.HttpStatusCode</c> (418 and the non-standard ones).
    /// </summary>
    private static readonly int[] CodesOutsideHttpStatusCodeEnum = [418, 499, 599];

    private static readonly string[] SetModes =
    [
        "ok",
        "bad",
        "bad-empty",
        "missing",
        "missing-empty",
        "conflict",
        "invalid",
        "invalid-empty",
        "throw",
        "throw-argument",
        "throw-invalid-operation",
        "throw-timeout",
        "throw-canceled",
        "throw-unauthorized-access",
        "throw-argument-null",
        "throw-bad-http-request",
    ];

    private static readonly string[] FaultModes =
    [
        "ok",
        "error500",
        "error500-empty",
        "unavailable",
        "throw",
        "throw-not-implemented",
    ];

    // d{code}: the handler returns the spec's `default` body with that status (every error status code).
    private static readonly string[] DefaultErrorModes =
    [
        "ok",
        "error",
        "throw",
        .. AllErrorStatusCodes.Select(c => $"d{c}"),
    ];

    /// <summary>
    /// Error responses the generated server never writes - a proxy's page, another framework's body, a malformed or
    /// untyped one - served on the <c>getFault</c> route (<c>/faults/stub-…</c>: 503 is declared without content, 502
    /// is not declared) and on the <c>setAllJsonProblem</c> route (every status declared as the <c>ApiProblem</c>
    /// model), so every generated client reads them.
    /// </summary>
    internal static readonly Dictionary<string, (int Status, string? ContentType, string Body, string Description)> StubResponses = new(StringComparer.Ordinal)
    {
        ["stub-xml"] = (503, "application/xml", "<error><message>xml down</message></error>", "XML"),
        ["stub-octet"] = (503, "application/octet-stream", "binary down", "application/octet-stream"),
        ["stub-untyped"] = (503, null, "untyped down", "a body without a Content-Type"),
        ["stub-empty"] = (503, null, string.Empty, "no body, no Content-Type"),
        ["stub-html"] = (502, "text/html; charset=utf-8", "<html><body><h1>502 Bad Gateway</h1></body></html>", "a proxy's HTML page (undeclared 502)"),
        ["stub-invalid-json"] = (503, "application/json", "{\"detail\": \"cut", "malformed JSON"),
        ["stub-empty-json"] = (503, "application/json", string.Empty, "an empty body under application/json"),
        ["stub-blank-json"] = (503, "application/json", "   ", "a whitespace body under application/json"),
        ["stub-problem-casing"] = (503, "Application/Problem+JSON; charset=utf-8", "{\"title\":\"Casing\",\"status\":503,\"detail\":\"casing down\"}", "a ProblemDetails under a mixed-case media type"),
        ["stub-vendor-json"] = (503, "application/vnd.error+json", "{\"message\":\"vendor down\"}", "a vendor +json object"),
        ["stub-utf8-text"] = (503, "text/plain; charset=utf-8", "nede: æøå – 🚫", "non-ASCII text"),
        ["stub-json-string"] = (503, "application/json", "\"42\"", "a JSON string that reads like a number"),
    };

    /// <summary>
    /// Every <c>System.Net.HttpStatusCode</c> value below 400 (1xx, 2xx, 3xx, aliases counted once). Each has its own
    /// operation <c>getStatus{code}</c> (<c>/statuses/{code}/{mode}</c>) - an operation declares one 2xx
    /// (ATC_API_OPR018).
    /// </summary>
    internal static readonly int[] NonErrorStatusCodes = [.. Enum.GetValues<HttpStatusCode>().Select(c => (int)c).Where(c => c < 400).Distinct().Order()];

    /// <summary>
    /// The statuses below 400 declared with a JSON body (<c>Item</c>): the 2xx that carry content, and 300.
    /// </summary>
    internal static readonly int[] StatusCodesWithBody = [200, 201, 202, 203, 206, 207, 208, 226, 300];

    /// <summary>
    /// The redirects: declared with a <c>Location</c> header, no body.
    /// </summary>
    internal static readonly int[] RedirectStatusCodes = [301, 302, 303, 307, 308];

    internal static readonly string[] ErrorResponseFormats =
    [
        "ProblemDetails",
        "PlainText",
        "PlainTextOnly",
        "Custom",
    ];

    internal static readonly string[] TypedResultStyles =
    [
        "Throw",
        "Result",
    ];

    internal static string Yaml { get; } = BuildYaml();

    internal static string ClientOperationMarker(string errorResponseFormat)
        => string.Equals(errorResponseFormat, "Custom", StringComparison.Ordinal)
            ? """
              {
                "generationMode": "EndpointPerOperation",
                "errorResponseFormat": "Custom",
                "customErrorResponseModel": {
                  "name": "ApiError",
                  "schema": {
                    "code": { "type": "string" },
                    "message": { "type": "string" }
                  }
                }
              }
              """
            : $$"""
              {
                "generationMode": "EndpointPerOperation",
                "errorResponseFormat": "{{errorResponseFormat}}"
              }
              """;

    internal static string ClientTypedMarker(string resultStyle)
        => $$"""
          {
            "generationMode": "TypedClient",
            "typedClientResultStyle": "{{resultStyle}}"
          }
          """;

    /// <summary>
    /// Writes every generated source to disk, for reading the output by hand.
    /// </summary>
    [Fact(Explicit = true)]
    public void Dump_GeneratedSources()
    {
        var root = Environment.GetEnvironmentVariable("ERRORBODY_DUMP_DIR") ?? Path.Combine(Path.GetTempPath(), "errorbody-dump");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, ProjectName + ".yaml"), Yaml);
        Dump(Path.Combine(root, "Server"), GenerateServer());
        foreach (var format in ErrorResponseFormats)
        {
            Dump(Path.Combine(root, "Client-" + format), Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker(format)));
        }

        foreach (var style in TypedResultStyles)
        {
            Dump(Path.Combine(root, "Typed-" + style), Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientTypedMarker(style)));
        }
    }

    /// <summary>
    /// Hosts the generated server, sends every case over the wire and through every generated
    /// client, and writes the full matrix as markdown.
    /// </summary>
    [Fact(Explicit = true)]
    public async Task Report_WireAndClientMatrix()
    {
        var path = Environment.GetEnvironmentVariable("ERRORBODY_REPORT_FILE") ?? Path.Combine(Path.GetTempPath(), "errorbody-report.md");

        await using var host = await ErrorBodyHost.StartAsync();

        var sb = new StringBuilder();
        sb.AppendLine("# Error body matrix (generated by ErrorResponseBodyRuntimeTests)");
        sb.AppendLine();
        sb.AppendLine("## Server factory methods");
        sb.AppendLine();
        foreach (var (operationId, factories) in host.Factories.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- `{operationId}`: {string.Join(", ", factories.Select(f => $"`{f.Key}({f.Value.Signature})`"))}");
        }

        sb.AppendLine();
        sb.AppendLine("## Server .Produces metadata");
        sb.AppendLine();
        foreach (var line in host.ProducesMetadata)
        {
            sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine("## Runtime endpoint response metadata (what an OpenAPI document is built from)");
        sb.AppendLine();
        foreach (var line in host.DescribeEndpointResponseMetadata())
        {
            sb.AppendLine(line);
        }

        var wireResults = await host.CaptureWireAsync();
        var clients = new Dictionary<string, (GeneratedClient Client, List<string[]> Rows)>(StringComparer.Ordinal);
        foreach (var format in ErrorResponseFormats)
        {
            var client = GenerateClientAssembly(ClientOperationMarker(format), BuildOperationProbeSource);
            clients[format] = (client, await RunProbeAsync(client.Assembly, host.BaseAddress));
        }

        AppendStatusSweep(sb, host, wireResults, clients);

        sb.AppendLine();
        sb.AppendLine("## Wire");
        sb.AppendLine();
        sb.AppendLine("| Case | Status | Content-Type | Body |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var wire in wireResults)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {wire.Case} | {wire.Status} | {wire.ContentType} | `{Cell(wire.Body)}` |");
        }

        foreach (var format in ErrorResponseFormats)
        {
            var (client, rows) = clients[format];
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"## Client EndpointPerOperation — errorResponseFormat: {format}");
            sb.AppendLine();
            sb.AppendLine("Registrations:");
            sb.AppendLine();
            foreach (var line in client.Registrations)
            {
                sb.AppendLine(line);
            }

            sb.AppendLine();
            sb.AppendLine("| Case | Status | IsSuccess | ContentObject | Typed accessor |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var row in rows)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {row[0]} | {row[1]} | {row[2]} | `{Cell(row[3])}` | `{Cell(row[4])}` |");
            }
        }

        foreach (var style in TypedResultStyles)
        {
            var client = GenerateClientAssembly(ClientTypedMarker(style), cases => BuildTypedProbeSource(cases, style));
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"## Client TypedClient — typedClientResultStyle: {style}");
            sb.AppendLine();
            sb.AppendLine("| Case | Outcome |");
            sb.AppendLine("|---|---|");
            foreach (var row in await RunProbeAsync(client.Assembly, host.BaseAddress))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {row[0]} | `{Cell(row[1])}` |");
            }
        }

        await File.WriteAllTextAsync(path, sb.ToString(), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// One row per status code for the <c>all-*</c> operations: the factory the server generated, what
    /// it put on the wire with and without a message, and what the default and PlainText clients read.
    /// </summary>
    private static void AppendStatusSweep(
        StringBuilder sb,
        ErrorBodyHost host,
        List<WireResult> wireResults,
        Dictionary<string, (GeneratedClient Client, List<string[]> Rows)> clients)
    {
        var wire = wireResults.ToDictionary(w => w.Case, StringComparer.Ordinal);
        var problemDetailsClient = clients["ProblemDetails"].Rows.ToDictionary(r => r[0], StringComparer.Ordinal);
        var plainTextClient = clients["PlainText"].Rows.ToDictionary(r => r[0], StringComparer.Ordinal);

        string Factory(
            string operationId,
            int code)
            => host.Factories[operationId].TryGetValue(HttpStatusCodeHelper.ToEnumName(code), out var f)
                ? $"{HttpStatusCodeHelper.ToEnumName(code)}({f.Signature})"
                : "(no factory)";

        string Wire(string caseName)
            => wire.TryGetValue(caseName, out var w)
                ? $"{w.Status} {w.ContentType.Split(';')[0]} {Short(w.Body)}"
                : "-";

        static string Accessor(
            Dictionary<string, string[]> rows,
            string caseName)
            => rows.TryGetValue(caseName, out var r) ? Short(r[4]) : "-";

        sb.AppendLine();
        sb.AppendLine("## Status sweep (every 4xx/5xx)");
        sb.AppendLine();
        sb.AppendLine("`all-none` declares every code with no content; `all-json-problem` declares every code with `application/json` + `ApiProblem`.");
        sb.AppendLine();
        sb.AppendLine("| Code | Factory (all-none) | Factory (all-json-problem) | Wire all-none, with message | Wire all-none, empty | Wire all-json-problem, with argument | Default client (all-none, message) | PlainText client (all-none, message) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var code in AllErrorStatusCodes)
        {
            var noneCase = $"setAllNone/s{code}";
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {code} | `{Cell(Factory("setAllNone", code))}` | `{Cell(Factory("setAllJsonProblem", code))}` | `{Cell(Wire(noneCase))}` | `{Cell(Wire(noneCase + "-empty"))}` | `{Cell(Wire($"setAllJsonProblem/s{code}"))}` | `{Cell(Accessor(problemDetailsClient, noneCase))}` | `{Cell(Accessor(plainTextClient, noneCase))}` |");
        }
    }

    private static string Short(string value)
        => value.Length <= 110 ? value : value[..110] + "…";

    /// <summary>
    /// <c>.Produces(StatusCodes.{constant})</c> is emitted for every declared status, so each constant
    /// <c>HttpStatusCodeHelper</c> maps to has to exist on ASP.NET Core's <c>StatusCodes</c> -
    /// otherwise a spec that merely declares that status produces a server that does not compile.
    /// (2.1.0 mapped 103 and 425 to <c>Status103EarlyHints</c> / <c>Status425TooEarly</c>, neither of which exists.)
    /// </summary>
    [Fact]
    public void StatusCodesConstants_AllExistOnAspNetCore()
    {
        var missing = Enumerable
            .Range(100, 500)
            .Select(code => HttpStatusCodeHelper.TryGetStatusCodesConstant(code.ToString(CultureInfo.InvariantCulture), out var constant) ? constant : null)
            .OfType<string>()
            .Where(constant => typeof(StatusCodes).GetField(constant) is null)
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// Responses declared as <c>$ref: '#/components/responses/...'</c> are resolved like inline ones: the
    /// handler gets a factory for them, and they reach the endpoint metadata. 2.1.0 silently dropped them
    /// on the server (<c>response.Value is not OpenApiResponse</c> skipped the reference type).
    /// </summary>
    [Fact]
    public async Task RefResponses_GetFactoriesAndEndpointMetadata()
    {
        await using var host = await ErrorBodyHost.StartAsync();

        Assert.True(host.Factories["setRefResponse"].ContainsKey("NotFound"));
        Assert.True(host.Factories["setRefResponse"].ContainsKey("UnprocessableEntity"));

        var statusCodes = host.ResponseStatusCodes("SetRefResponse");
        Assert.Contains(404, statusCodes);
        Assert.Contains(422, statusCodes);
    }

    /// <summary>
    /// A schema referenced only through <c>components/responses</c> is still generated, on the server and
    /// the client (it would otherwise be referenced but missing).
    /// </summary>
    [Fact]
    public void SchemaReferencedOnlyThroughComponentResponses_IsGenerated()
    {
        var server = GenerateServer();
        var client = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker("ProblemDetails"));

        Assert.Contains(server, s => s.HintName.EndsWith(".RefOnlyError.g.cs", StringComparison.Ordinal));
        Assert.Contains(client, s => s.HintName.EndsWith(".RefOnlyError.g.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// Every status an operation declares must reach the endpoint's response metadata (what an OpenAPI
    /// document is built from) - including codes that have no <c>StatusCodes</c> constant (425, 499, 599),
    /// which 2.1.0 silently skipped.
    /// </summary>
    [Fact]
    public async Task EveryDeclaredErrorStatus_IsInTheEndpointResponseMetadata()
    {
        await using var host = await ErrorBodyHost.StartAsync();

        var declared = host.ResponseStatusCodes("SetAllNone")
            .Where(code => code >= 400)
            .Distinct()
            .Order();

        Assert.Equal(AllErrorStatusCodes.Order(), declared);
    }

    /// <summary>
    /// A client generated from a spec that declares every 4xx/5xx - including codes outside the
    /// <c>System.Net.HttpStatusCode</c> enum (418, 499, 599) - must compile, in every error format.
    /// 2.1.0 emitted <c>HttpStatusCode.Status418</c>, which is not an enum member (CS0117).
    /// </summary>
    [Theory]
    [InlineData("ProblemDetails")]
    [InlineData("PlainText")]
    [InlineData("PlainTextOnly")]
    [InlineData("Custom")]
    public void ClientForEveryErrorStatus_Compiles(string errorResponseFormat)
    {
        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker(errorResponseFormat));
        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources);

        Assert.Empty(errors);
        foreach (var code in CodesOutsideHttpStatusCodeEnum)
        {
            Assert.Contains(sources, s => s.Source.Contains($"(HttpStatusCode){code}", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Every handler call shape that compiled against 2.x error factories - for a spec that declares no
    /// error bodies, and for the declared-model 400 / default - still compiles against the 3.0 factories
    /// (no removed overloads, no CS0121 ambiguities).
    /// </summary>
    [Fact]
    public void HandlerCallShapesFrom2x_StillCompile()
    {
        const string calls = """
            using Microsoft.AspNetCore.Mvc;
            using ErrorBodies.Generated.Models;
            using None = ErrorBodies.Generated.AllNone.Results.SetAllNoneResult;
            using Custom = ErrorBodies.Generated.JsonCustom.Results.SetJsonCustomResult;
            using Fallback = ErrorBodies.Generated.DefaultError.Results.GetDefaultErrorResult;

            namespace ErrorBodies.CompatCheck;

            public static class HandlerCallShapes
            {
                public static object[] All()
                    =>
                    [
                        None.BadRequest(),
                        None.BadRequest(null),
                        None.BadRequest(new ValidationProblemDetails()),
                        None.Unauthorized(),
                        None.Forbidden(),
                        None.NotFound(),
                        None.NotFound(null),
                        None.NotFound("m"),
                        None.Conflict(),
                        None.Conflict("m"),
                        None.PreconditionFailed(),
                        None.UnprocessableEntity(),
                        None.UnprocessableEntity("m"),
                        None.TooManyRequests(),
                        None.TooManyRequests(30),
                        None.InternalServerError(),
                        None.InternalServerError(null),
                        None.InternalServerError(new ProblemDetails()),
                        None.GatewayTimeout(),
                        None.GatewayTimeout(new ProblemDetails()),
                        None.ServiceUnavailable(),
                        None.ServiceUnavailable("m"),
                        Custom.BadRequest(),
                        Custom.BadRequest(new ErrorResponse("c", "m")),
                        Fallback.Error(new ErrorResponse("c", "m")),
                    ];
            }
            """;

        var sources = GenerateServer();
        sources.Add(("HandlerCallShapes.cs", calls));

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true);

        Assert.Empty(errors);
    }

    /// <summary>
    /// Where the spec declares a <c>ProblemDetails</c> body, a factory has both the 2.x shape
    /// (<c>X(string? message)</c>, 429 <c>X(int? retryAfterSeconds, string? message)</c>, default
    /// <c>Error(string? message, int statusCode)</c>) and an <c>X(ProblemDetails problem)</c> overload.
    /// <c>X(null)</c> compiled in 2.x and must still compile, resolving to the 2.x shape.
    /// </summary>
    [Fact]
    public void DeclaredProblemDetails_NullArgumentCallsFrom2x_StillCompile()
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: ErrorBodies
              version: 1.0.0
            paths:
              /things/{thingId}:
                get:
                  operationId: getThing
                  parameters:
                    - name: thingId
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '204':
                      description: No Content
                    '404':
                      description: Not Found
                      content:
                        application/problem+json:
                          schema:
                            $ref: '#/components/schemas/ProblemDetails'
                    '409':
                      description: Conflict
                      content:
                        application/problem+json:
                          schema:
                            $ref: '#/components/schemas/ProblemDetails'
                    '429':
                      description: Too Many Requests
                      content:
                        application/problem+json:
                          schema:
                            $ref: '#/components/schemas/ProblemDetails'
                    default:
                      description: Error
                      content:
                        application/problem+json:
                          schema:
                            $ref: '#/components/schemas/ProblemDetails'
            components:
              schemas:
                ProblemDetails:
                  type: object
                  properties:
                    title:
                      type: string
                    status:
                      type: integer
                    detail:
                      type: string
            """;

        const string calls = """
            using Microsoft.AspNetCore.Mvc;
            using Thing = ErrorBodies.Generated.Results.GetThingResult;

            namespace ErrorBodies.CompatCheck;

            public static class NullArgumentCalls
            {
                public static object[] All()
                    =>
                    [
                        Thing.NotFound(),
                        Thing.NotFound(null),
                        Thing.NotFound("m"),
                        Thing.NotFound(new ProblemDetails()),
                        Thing.Conflict(null),
                        Thing.TooManyRequests(),
                        Thing.TooManyRequests(null),
                        Thing.TooManyRequests(30),
                        Thing.TooManyRequests(null, "m"),
                        Thing.TooManyRequests(new ProblemDetails(), 30),
                        Thing.Error(),
                        Thing.Error(null),
                        Thing.Error(null, 503),
                        Thing.Error("m", 503),
                        Thing.Error(new ProblemDetails(), 503),
                    ];
            }
            """;

        var sources = Generate(new ApiServerGenerator(), ".atc-rest-api-server", ServerMarker, includeMinimalApi: true, yaml);
        sources.Add(("NullArgumentCalls.cs", calls));

        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true);

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors.Concat(sources.Select(s => s.HintName))));
    }

    /// <summary>
    /// Error-body shapes that compiled with 2.x (which ignored most declared error bodies) must compile with the
    /// 3.0 contract, on the server and the EndpointPerOperation client:
    /// <list type="bullet">
    ///   <item>a spec <c>ProblemDetails</c> that another model extends (<c>allOf</c>) or holds as a property;</item>
    ///   <item>an error body that <c>$ref</c>s a primitive alias (<c>ErrorMessage: string</c>, <c>ErrorNumber: integer</c>);</item>
    ///   <item>an inline error schema on a spec whose paths share one first segment;</item>
    ///   <item>an array of inline objects as error body next to one as success body (distinct item types);</item>
    ///   <item>a media type with a quoted parameter; a declared non-400 <c>ValidationProblemDetails</c>;</item>
    ///   <item>the 2.x call <c>TooManyRequests(30)</c> on a 429 that declares a model.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void ErrorBodyShapesThatCompiledIn2x_StillCompile()
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: ErrorBodies
              version: 1.0.0
            paths:
              /things/{thingId}:
                get:
                  operationId: getThing
                  parameters:
                    - name: thingId
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              type: object
                              properties:
                                id:
                                  type: string
                                name:
                                  type: string
                    '400':
                      description: Bad Request
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              type: object
                              properties:
                                field:
                                  type: string
                                message:
                                  type: string
                    '404':
                      description: Not Found
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/ErrorMessage'
                    '409':
                      description: Conflict
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/ErrorNumber'
                    '410':
                      description: Gone
                      content:
                        application/json; profile="urn:x":
                          schema:
                            $ref: '#/components/schemas/ApiProblem'
                    '422':
                      description: Unprocessable
                      content:
                        application/problem+json:
                          schema:
                            $ref: '#/components/schemas/ValidationProblemDetails'
                    '429':
                      description: Too Many Requests
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/ErrorEnvelope'
                    '503':
                      description: Unavailable
                      content:
                        application/json:
                          schema:
                            type: object
                            properties:
                              reason:
                                type: string
            components:
              schemas:
                ProblemDetails:
                  type: object
                  properties:
                    title:
                      type: string
                    detail:
                      type: string
                ValidationProblemDetails:
                  type: object
                ApiProblem:
                  allOf:
                    - $ref: '#/components/schemas/ProblemDetails'
                    - type: object
                      properties:
                        code:
                          type: string
                ErrorEnvelope:
                  type: object
                  properties:
                    problem:
                      $ref: '#/components/schemas/ProblemDetails'
                ErrorMessage:
                  type: string
                ErrorNumber:
                  type: integer
            """;

        const string calls = """
            using Microsoft.AspNetCore.Mvc;
            using Thing = ErrorBodies.Generated.Results.GetThingResult;

            namespace ErrorBodies.CompatCheck;

            public static class ErrorBodyCalls
            {
                public static object[] All()
                    =>
                    [
                        Thing.NotFound("not found"),
                        Thing.Conflict(5),
                        Thing.TooManyRequests(),
                        Thing.TooManyRequests(30),
                        Thing.UnprocessableEntity(new ValidationProblemDetails()),
                    ];
            }
            """;

        var server = Generate(new ApiServerGenerator(), ".atc-rest-api-server", ServerMarker, includeMinimalApi: true, yaml);
        server.Add(("ErrorBodyCalls.cs", calls));
        var serverErrors = CompilationVerificationHarness.CompileGeneratedSources(server, includeMinimalApi: true);
        Assert.True(serverErrors.Count == 0, "Server: " + string.Join(Environment.NewLine, serverErrors));

        // The spec's ProblemDetails is a model (ApiProblem extends it), and the error array item is its own type.
        Assert.Contains(server, s => s.Source.Contains("record ProblemDetails(", StringComparison.Ordinal));
        Assert.Contains(server, s => s.Source.Contains("GetThingBadRequestErrorItem", StringComparison.Ordinal));

        var client = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, yaml);
        var clientErrors = CompilationVerificationHarness.CompileGeneratedSources(client);
        Assert.True(clientErrors.Count == 0, "Client: " + string.Join(Environment.NewLine, clientErrors));
    }

    /// <summary>
    /// A spec that defines its own <c>ProblemDetails</c> schema (a common convention) must still give a
    /// compiling server. The name binds to <c>Microsoft.AspNetCore.Mvc.ProblemDetails</c>, which every
    /// generated Results file imports; 2.1.0 also emitted a <c>Models.ProblemDetails</c> record, making
    /// every <c>ProblemDetails</c> reference ambiguous (CS0104) - even the implicit default of an
    /// undeclared-content 500 on an operation that never mentions the schema.
    /// </summary>
    [Fact]
    public void SpecDefinedProblemDetailsSchema_ServerCompilesAgainstTheFrameworkType()
    {
        var yaml = Yaml.Replace("ApiProblem", "ProblemDetails", StringComparison.Ordinal);

        var sources = Generate(new ApiServerGenerator(), ".atc-rest-api-server", ServerMarker, includeMinimalApi: true, yaml);
        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true);

        Assert.Empty(errors);
        Assert.DoesNotContain(sources, s => s.HintName.EndsWith(".Models.ProblemDetails.g.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// The 3.0 error contract, over every declared 4xx/5xx status and every declaration style, called by
    /// a handler both with a value and with nothing:
    /// <list type="bullet">
    ///   <item>nothing declared, or a schema named <c>ProblemDetails</c>: an RFC 9457 ProblemDetails as
    ///   <c>application/problem+json</c> whose <c>status</c> is the response status (the value, when given,
    ///   ends up in it); 429 carries <c>Retry-After</c>;</item>
    ///   <item>an <c>application/json</c> object schema: that object as <c>application/json</c>;</item>
    ///   <item>an <c>application/json</c> string: the JSON string;</item>
    ///   <item><c>text/plain</c>: the raw text.</item>
    /// </list>
    /// A declared body that the handler leaves empty gets a ProblemDetails from the generated host wiring.
    /// </summary>
    [Fact]
    public async Task Sweep_WireMatchesTheErrorContract()
    {
        await using var host = await ErrorBodyHost.StartAsync();

        var wire = (await host.CaptureWireAsync()).ToDictionary(w => w.Case, StringComparer.Ordinal);
        var mismatches = host.Cases
            .Where(c => SweepStatus(c.Mode) is not null && c.Reason == ValidReason)
            .Select(c => CheckErrorContract(c, wire[c.Name]))
            .OfType<string>()
            .ToList();

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} contract mismatches:\n" + string.Join("\n", mismatches));
    }

    /// <summary>
    /// Errors raised by the framework before any generated code runs (malformed JSON, an unknown route, a
    /// wrong method) get an RFC 9457 ProblemDetails from the generated host wiring
    /// (<c>AddProblemDetails()</c> + <c>UseStatusCodePages()</c>); 2.x sent them with an empty body.
    /// </summary>
    [Fact]
    public async Task Host_FrameworkErrors_AreProblemDetails()
    {
        await using var host = await ErrorBodyHost.StartAsync();

        var framework = (await host.CaptureWireAsync())
            .Where(w => w.Case.StartsWith("framework:", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(4, framework.Count);
        Assert.All(framework, w =>
        {
            Assert.Equal("application/problem+json", w.MediaType);
            Assert.True(TryReadStatus(w.Body, out var status) && status == w.Status, w.Case);
        });

        // A missing required body is reported under the empty key with MVC's message - not under the
        // parameters record's C# member name (Request), which the client never sent.
        var missingBody = framework.Single(w => w.Case == "framework: missing required body");
        using var document = JsonDocument.Parse(missingBody.Body);
        var errors = document.RootElement.GetProperty("errors");
        Assert.Equal([string.Empty], errors.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
        Assert.Equal(["A non-empty request body is required."], errors.GetProperty(string.Empty).EnumerateArray().Select(m => m.GetString()), StringComparer.Ordinal);
    }

    /// <summary>
    /// The host wiring can be switched off through the generated options; the framework then writes its
    /// empty bodies again.
    /// </summary>
    [Fact]
    public async Task Host_ProblemDetailsWiring_CanBeTurnedOff()
    {
        await using var host = await ErrorBodyHost.StartAsync(problemDetailsWiringOff: true);

        var unknownRoute = (await host.CaptureWireAsync())
            .Single(w => w.Case == "framework: unknown route");

        Assert.Equal(404, unknownRoute.Status);
        Assert.Equal("(empty)", unknownRoute.Body);
    }

    /// <summary>
    /// A client generated (default <c>errorResponseFormat</c>) from the same spec as the server reads every
    /// error the server writes as the declared type, with no exception: ProblemDetails for undeclared and
    /// problem responses (<c>ValidationProblemDetails</c> for an undeclared 400), the declared model - also
    /// when it is declared under a <c>+json</c> media type such as <c>application/problem+json</c> - and the
    /// declared string, unquoted.
    /// </summary>
    [Fact]
    public async Task ClientDefaultFormat_ReadsWhatTheServerWrites()
    {
        await using var host = await ErrorBodyHost.StartAsync();
        var client = GenerateClientAssembly(ClientOperationMarker("ProblemDetails"), BuildOperationProbeSource);
        var rows = await RunProbeAsync(client.Assembly, host.BaseAddress);
        var wire = (await host.CaptureWireAsync()).ToDictionary(w => w.Case, StringComparer.Ordinal);

        var mismatches = new List<string>();
        foreach (var row in rows)
        {
            var (name, accessor) = (row[0], row[4]);
            var operationId = name.Split('/')[0];
            var mode = name.Split('/')[1].Split(' ')[0];
            var code = SweepStatus(mode);
            var withValue = !mode.EndsWith("-empty", StringComparison.Ordinal);

            string? expected = (operationId, code) switch
            {
                ("setAllNone" or "setAllFrameworkProblem", 400) => "ValidationProblemDetails ",
                ("setAllNone" or "setAllFrameworkProblem", not null) => "ProblemDetails ",
                ("setAllJsonProblem", not null) => "ApiProblem ",
                ("setAllJsonString" or "setAllTextPlain", not null) when withValue => $"string \"{code} message from handler\"",

                // No body from the handler: the host wiring's ProblemDetails, whose message (detail, else title) the
                // string accessor gives - not the ProblemDetails' JSON text.
                ("setAllJsonString" or "setAllTextPlain", not null) => $"string {JsonSerializer.Serialize(ProblemMessage(wire[name].Body))}",
                ("setProblemJson", null) when mode is "bad" or "missing" or "conflict" or "invalid" => "ApiProblem ",
                _ => null,
            };

            // An inline (anonymous) error schema is read as its generated model type.
            if (operationId == "setInlineError" && mode is "bad" or "missing" or "conflict" or "invalid")
            {
                if (accessor.Contains(" THROWS ", StringComparison.Ordinal) ||
                    accessor.Contains("Content => string", StringComparison.Ordinal) ||
                    accessor.Contains("Content => Object", StringComparison.Ordinal) ||
                    accessor.Contains("Content => JsonElement", StringComparison.Ordinal) ||
                    !accessor.Contains("error from handler", StringComparison.Ordinal))
                {
                    mismatches.Add($"{name}: expected the generated inline model, got '{Short(accessor)}'");
                }

                continue;
            }

            if (expected is null)
            {
                continue;
            }

            if (accessor.Contains(" THROWS ", StringComparison.Ordinal) ||
                !accessor.Contains($"Content => {expected}", StringComparison.Ordinal))
            {
                mismatches.Add($"{name}: expected '{expected}', got '{Short(accessor)}'");
            }
        }

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} client mismatches:\n" + string.Join("\n", mismatches));
    }

    /// <summary>
    /// The message of a ProblemDetails body: its <c>detail</c>, else its <c>title</c>, else empty.
    /// </summary>
    internal static string ProblemMessage(string body)
    {
        var problem = JsonNode.Parse(body)!.AsObject();
        return problem["detail"] is JsonValue detail && ((string?)detail)?.Length > 0
            ? (string)detail!
            : (string?)problem["title"] ?? string.Empty;
    }

    /// <summary>
    /// The spec's <c>default</c> response on the EndpointPerOperation client, over every error status code:
    /// the handler's <c>Error(body, status)</c> is read as the default model (<c>IsDefaultError</c> /
    /// <c>DefaultErrorContent</c>) - also for 400/404/500/504, which the client registers as ProblemDetails
    /// for the framework's own errors - while an unhandled exception (the framework's 500 ProblemDetails,
    /// sent as <c>application/problem+json</c>) is still read as a ProblemDetails.
    /// </summary>
    [Fact]
    public async Task ClientDefaultResponse_EveryStatus_IsReadAsTheDefaultBody()
    {
        await using var host = await ErrorBodyHost.StartAsync();
        var client = GenerateClientAssembly(ClientOperationMarker("ProblemDetails"), BuildOperationProbeSource);
        var rows = (await RunProbeAsync(client.Assembly, host.BaseAddress))
            .Where(r => r[0].StartsWith("getDefaultError/", StringComparison.Ordinal))
            .ToDictionary(r => r[0].Split('/')[1].Split(' ')[0], StringComparer.Ordinal);

        var mismatches = new List<string>();
        foreach (var code in AllErrorStatusCodes)
        {
            var row = rows[$"d{code}"];
            var expected = $"DefaultErrorContent => ErrorResponse {{\"Code\":\"c{code}\",\"Message\":\"{code} default from handler\"}}";
            if (row[1] != code.ToString(CultureInfo.InvariantCulture) || !row[4].Contains(expected, StringComparison.Ordinal))
            {
                mismatches.Add($"d{code}: expected {code} and '{expected}', got {row[1]} '{Short(row[4])}'");
            }
        }

        var thrown = rows["throw"];
        if (thrown[1] != "500" || !thrown[4].StartsWith("InternalServerErrorContent => ProblemDetails ", StringComparison.Ordinal))
        {
            mismatches.Add($"throw: expected the framework's 500 ProblemDetails, got {thrown[1]} '{Short(thrown[4])}'");
        }

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} default-response mismatches:\n" + string.Join("\n", mismatches));
    }

    /// <summary>
    /// The <c>default</c> response where the media type cannot tell the framework's ProblemDetails from the
    /// handler's default body, answered by a stub server over the generator-added statuses (400, 404, 500) and
    /// an unlisted one (418):
    /// <list type="bullet">
    ///   <item>a problem-shaped default (<c>application/problem+json</c>): every error status is read as the
    ///   default model, and the ProblemDetails accessors of the generator-added statuses read the body again
    ///   instead of throwing;</item>
    ///   <item>a plain JSON default, while the API answers with a ProblemDetails sent as <c>application/json</c>:
    ///   the ProblemDetails accessors still give that ProblemDetails;</item>
    ///   <item><c>errorResponseFormat: Custom</c>: the declared default body is the default error on the
    ///   generator-added statuses too, and their custom-model accessors read the same body.</item>
    /// </list>
    /// </summary>
    [Theory]
    [InlineData("ProblemDetails")]
    [InlineData("Custom")]
    public async Task ClientDefaultResponse_WithoutAMediaTypeSplit_KeepsEveryAccessorReadable(
        string errorResponseFormat)
    {
        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker(errorResponseFormat), includeMinimalApi: false, DefaultOnlyYaml);
        sources.Add(("Probe.cs", DefaultOnlyProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var probe = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!;
        var rows = await (Task<List<string>>)probe.GetMethod("RunAsync")!.Invoke(null, [])!;
        var report = string.Join("\n", rows);

        Assert.True(!report.Contains("THROWS", StringComparison.Ordinal), report);
        AssertRow("problemDefault/418: 418 IsDefaultError DefaultErrorContent => ApiProblem .*\"code\":\"E418\"");
        AssertRow("jsonDefault/418: 418 IsDefaultError DefaultErrorContent => ErrorResponse .*\"code\":\"E418\"");

        (int Code, string Name)[] generatorAdded = [(400, "BadRequest"), (404, "NotFound"), (500, "InternalServerError")];
        foreach (var (code, name) in generatorAdded)
        {
            if (string.Equals(errorResponseFormat, "Custom", StringComparison.Ordinal))
            {
                // The custom model stands for the generator-added statuses, yet the declared default body is the
                // default error, as with every format: a problem-shaped default always, a JSON default because the
                // body is not application/problem+json. The custom accessor still reads the same body.
                AssertRow($"problemDefault/{code}: {code} IsDefaultError DefaultErrorContent => ApiProblem .*\"code\":\"E{code}\"");
                AssertRow($"jsonDefault/{code}: {code} IsDefaultError DefaultErrorContent => ErrorResponse .*\"code\":\"E{code}\"");
                AssertRow($"problemDefault/{code}: {code} Is{name} {name}Content => ApiError .*\"code\":\"E{code}\"");
                AssertRow($"jsonDefault/{code}: {code} Is{name} {name}Content => ApiError .*\"code\":\"E{code}\"");
                continue;
            }

            // A problem-shaped default: the status is the default body, and its ProblemDetails accessor still reads.
            AssertRow($"problemDefault/{code}: {code} IsDefaultError DefaultErrorContent => ApiProblem .*\"code\":\"E{code}\"");
            AssertRow($"problemDefault/{code}: {code} Is{name} {name}Content => (Validation)?ProblemDetails .*\"detail\":\"{code} detail\"");

            // A ProblemDetails sent as application/json under a plain JSON default.
            AssertRow($"jsonDefault/{code}: {code} Is{name} {name}Content => (Validation)?ProblemDetails .*\"detail\":\"{code} detail\"");
        }

        AssertRow("pdDefault/418: 418 IsDefaultError DefaultErrorContent => ProblemDetails .*\"detail\":\"418 detail\"");

        // A proxy's HTML 502 is not the default model, so it is not IsDefaultError, and nothing throws. A
        // ProblemDetails default reads it as a ProblemDetails with the page as its detail.
        Assert.DoesNotContain(rows, r => r.StartsWith("problemDefault/html: 502 IsDefaultError", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, r => r.StartsWith("jsonDefault/html: 502 IsDefaultError", StringComparison.Ordinal));
        AssertRow("pdDefault/html: 502 IsDefaultError DefaultErrorContent => ProblemDetails .*502 Bad Gateway");
        if (!string.Equals(errorResponseFormat, "Custom", StringComparison.Ordinal))
        {
            // A ProblemDetails default: the validation 400 stays a ValidationProblemDetails with its errors.
            AssertRow("pdDefault/400: 400 IsBadRequest BadRequestContent => ValidationProblemDetails .*\"errors\":\\{\"name\":\\[\"name 400\"\\]\\}");
            Assert.DoesNotContain(rows, r => r.StartsWith("pdDefault/400: 400 IsDefaultError", StringComparison.Ordinal));
            AssertRow("pdDefault/404: 404 IsNotFound NotFoundContent => ProblemDetails .*\"detail\":\"404 detail\"");
            AssertRow("pdDefault/500: 500 IsInternalServerError InternalServerErrorContent => ProblemDetails .*\"detail\":\"500 detail\"");
        }

        void AssertRow(string pattern)
            => Assert.True(
                rows.Any(r => Regex.IsMatch(r, "^" + pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1))),
                $"No row matches '{pattern}':\n{report}");
    }

    /// <summary>
    /// The typed client (Throw style) puts the error body into the exception for every operation. 2.1.0
    /// generated GETs with a return value through <c>GetFromJsonAsync</c>, whose exception drops the body
    /// ("Response status code does not indicate success").
    /// </summary>
    [Fact]
    public async Task TypedClientThrow_KeepsTheErrorBodyForGets()
    {
        await using var host = await ErrorBodyHost.StartAsync();
        var client = GenerateClientAssembly(ClientTypedMarker("Throw"), cases => BuildTypedProbeSource(cases, "Throw"));
        var rows = await RunProbeAsync(client.Assembly, host.BaseAddress);

        var getFailures = rows
            .Where(r => r[0].StartsWith("getFault/", StringComparison.Ordinal) && !r[0].EndsWith("/ok", StringComparison.Ordinal) && !r[0].Contains("/stub-", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(getFailures);
        Assert.All(getFailures, r =>
        {
            Assert.StartsWith("HttpRequestException: HTTP ", r[1], StringComparison.Ordinal);
            Assert.Contains("\"status\":", r[1], StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// The typed client (Result style) keeps the error body of a binary (download) operation in <c>Content</c>, as
    /// for every other operation - 2.x left it empty.
    /// </summary>
    [Fact]
    public async Task TypedClientResult_KeepsTheErrorBodyForDownloads()
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: ErrorBodies
              version: 1.0.0
            paths:
              /files/{name}:
                get:
                  operationId: getFile
                  parameters:
                    - name: name
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
                      content:
                        application/octet-stream:
                          schema:
                            type: string
                            format: binary
            """;
        const string probe = """"
            using ErrorBodies.Generated;

            namespace ErrorBodies.RuntimeTest;

            public static class Probe
            {
                public static async Task<string> RunAsync()
                {
                    using var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") };
                    var result = await new ErrorBodiesClient(http).GetFileAsync(new GetFileParameters("missing"));
                    return $"{(int)result.StatusCode} IsSuccess={result.IsSuccess} Content={result.Content}";
                }

                private sealed class StubHandler : HttpMessageHandler
                {
                    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                        => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                        {
                            Content = new StringContent("""{"title":"Not Found","status":404,"detail":"no such file"}""", System.Text.Encoding.UTF8, "application/problem+json"),
                        });
                }
            }
            """";

        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientTypedMarker("Result"), includeMinimalApi: false, yaml);
        sources.Add(("Probe.cs", probe));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var run = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("RunAsync")!;
        var outcome = await (Task<string>)run.Invoke(null, [])!;

        Assert.Equal("""404 IsSuccess=False Content={"title":"Not Found","status":404,"detail":"no such file"}""", outcome);
    }

    /// <summary>
    /// A redirect the HttpClient does not follow (<c>AllowAutoRedirect = false</c>) is the redirect operation's own
    /// status: <c>Is{Status}</c> (no content), not <c>IsSuccess</c>, never an error accessor - the matrix's redirect cells
    /// show the default, where the client follows it.
    /// </summary>
    [Fact]
    public async Task Redirects_NotFollowed_AreTheirOwnAccessors()
    {
        await using var host = await ErrorBodyHost.StartAsync();
        var client = GenerateClientAssembly(ClientOperationMarker("ProblemDetails"), cases => WithoutRedirects(BuildOperationProbeSource(cases)));
        var rows = await RunProbeAsync(client.Assembly, host.BaseAddress);

        foreach (var code in RedirectStatusCodes)
        {
            var row = rows.Single(r => r[0] == $"getStatus{code}/s{code}");
            Assert.Equal(code.ToString(CultureInfo.InvariantCulture), row[1]);
            Assert.Equal("False", row[2]);
            Assert.Equal($"Is{HttpStatusCodeHelper.ToEnumName(code)} (no content property)", row[4]);
        }

        static string WithoutRedirects(string probe)
        {
            // The generated client's named HttpClient, configured last: its handler does not follow redirects.
            const string registration = "services.AddErrorBodiesEndpoints();";
            Assert.Contains(registration, probe, StringComparison.Ordinal);
            return probe.Replace(
                registration,
                registration + " services.AddHttpClient(Constants.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });",
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The Atc.Rest.Client the generated EndpointPerOperation client runs on reads an error body of any media type as
    /// text (<c>Content</c>), without the generated <c>WithTextErrorBody</c> fallback. 2.0.36 left <c>Content</c> empty for
    /// <c>application/xml</c> / <c>application/octet-stream</c>; this guards the fix released in 2.1.0.
    /// </summary>
    [Fact]
    public async Task AtcRestClient_ReadsAnErrorBodyOfAnyMediaTypeAsText()
    {
        using var provider = Atc.Rest.Client.Options.ServiceCollectionExtensions
            .AddAtcRestClientCore(new ServiceCollection())
            .BuildServiceProvider();
        var factory = provider.GetRequiredService<Atc.Rest.Client.Builder.IHttpMessageFactory>();

        foreach (var mediaType in (string[])["application/xml", "application/octet-stream"])
        {
            using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("<error>down</error>", Encoding.UTF8, mediaType),
            };

            var result = await factory
                .FromResponse(response)
                .BuildResponseAsync(r => r, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal("<error>down</error>", result.Content);
        }
    }

    /// <summary>
    /// A declared 1xx is never a final response (RFC 9110 §15.2): the server gets no factory and no endpoint metadata
    /// for it, and the EndpointPerOperation client no <c>Is…</c> accessor.
    /// </summary>
    [Fact]
    public void InformationalStatus_GetsNoFactoryMetadataOrAccessor()
    {
        var server = string.Join("\n", GenerateServer().Select(s => s.Source));
        var client = string.Join("\n", Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker("ProblemDetails")).Select(s => s.Source));

        foreach (var code in NonErrorStatusCodes.Where(c => c < 200))
        {
            var name = HttpStatusCodeHelper.ToEnumName(code);
            Assert.DoesNotContain($"public static GetStatus{code}Result {name}(", server, StringComparison.Ordinal);
            Assert.DoesNotContain($".Produces({HttpStatusCodeHelper.ToStatusCodesExpression(code)})", server, StringComparison.Ordinal);
            Assert.DoesNotContain($"public bool Is{name}", client, StringComparison.Ordinal);
        }

        // The statuses below 400 that are final responses do get theirs - e.g. a redirect takes its location.
        Assert.Contains("public static GetStatus303Result SeeOther(System.Uri location)", server, StringComparison.Ordinal);
        Assert.Contains("public static GetStatus206Result PartialContent(Item response)", server, StringComparison.Ordinal);
        Assert.Contains("public bool IsNotModified", client, StringComparison.Ordinal);
    }

    /// <summary>
    /// An error body Atc.Rest.Client kept as bytes (a media type that is neither JSON nor text) is decoded by the
    /// Content-Type charset - UTF-8 without one - and a success response is left alone.
    /// </summary>
    [Fact]
    public void ClientWithTextErrorBody_DecodesByTheContentTypeCharset()
    {
        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker("ProblemDetails"));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var withText = assembly.GetType("ErrorBodies.Generated.ProblemDetailsFactory", throwOnError: true)!.GetMethod("WithTextErrorBody")!;

        Atc.Rest.Client.EndpointResponse Decode(
            bool isSuccess,
            byte[] bytes,
            string contentType)
            => (Atc.Rest.Client.EndpointResponse)withText.Invoke(null, [
                new Atc.Rest.Client.EndpointResponse(
                    isSuccess,
                    isSuccess ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable,
                    string.Empty,
                    bytes,
                    new Dictionary<string, IEnumerable<string>>(StringComparer.Ordinal) { ["Content-Type"] = [contentType] }),
            ])!;

        const string text = "<fejl>æøå</fejl>";
        Assert.Equal(text, Decode(isSuccess: false, Encoding.Latin1.GetBytes(text), "application/xml; charset=iso-8859-1").Content);
        Assert.Equal(text, Decode(isSuccess: false, Encoding.UTF8.GetBytes(text), "application/xml").Content);
        Assert.Equal(text, Decode(isSuccess: false, Encoding.UTF8.GetBytes(text), "application/xml; charset=unknown-charset").Content);

        var success = Decode(isSuccess: true, Encoding.UTF8.GetBytes(text), "application/octet-stream");
        Assert.Equal(string.Empty, success.Content);
        Assert.IsType<byte[]>(success.ContentObject);
    }

    /// <summary>
    /// For third-party APIs that answer with a JSON string where ProblemDetails is expected, the generated
    /// <c>ProblemDetailsFactory</c> puts the string itself into <c>Detail</c> - not the JSON-quoted text
    /// (2.1.0 gave <c>Detail = "\"message\""</c>).
    /// </summary>
    [Fact]
    public void ClientProblemDetailsFactory_UnwrapsJsonStringBodies()
    {
        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker("ProblemDetails"));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var factory = assembly.GetType("ErrorBodies.Generated.ProblemDetailsFactory", throwOnError: true)!;
        var create = factory.GetMethod("Create")!;

        string? DetailOf(string message)
        {
            var problem = create.Invoke(null, [HttpStatusCode.NotFound, message])!;
            return (string?)problem.GetType().GetProperty("Detail")!.GetValue(problem);
        }

        Assert.Equal("item was not found", DetailOf("\"item was not found\""));
        Assert.Equal("item was not found", DetailOf("item was not found"));
        Assert.Equal("say \"hi\"", DetailOf("\"say \\\"hi\\\"\""));
    }

    /// <summary>
    /// The endpoint response metadata (what an OpenAPI document is built from) describes exactly what the
    /// error factories write, for every declared 4xx/5xx and every declaration style - one entry per
    /// status, with the body type and the content type.
    /// </summary>
    [Fact]
    public async Task Sweep_EndpointMetadataMatchesTheErrorContract()
    {
        await using var host = await ErrorBodyHost.StartAsync();

        var mismatches = new List<string>();
        foreach (var endpoint in new[] { "SetAllNone", "SetAllFrameworkProblem", "SetAllJsonProblem", "SetAllJsonString", "SetAllTextPlain" })
        {
            var metadata = host.ResponseMetadata(endpoint);
            foreach (var code in AllErrorStatusCodes)
            {
                (string TypeName, string ContentTypes) expected = endpoint switch
                {
                    "SetAllNone" when code == 400 => ("HttpValidationProblemDetails", "application/problem+json"),
                    "SetAllNone" or "SetAllFrameworkProblem" => ("ProblemDetails", "application/problem+json"),
                    "SetAllJsonProblem" => ("ApiProblem", "application/json"),
                    "SetAllJsonString" => ("String", "application/json"),
                    _ => ("String", "text/plain"),
                };

                var entries = metadata
                    .Where(m => m.StatusCode == code)
                    .Select(m => (m.TypeName, m.ContentTypes))
                    .ToList();
                if (entries.Count != 1 || entries[0] != expected)
                {
                    mismatches.Add($"{endpoint} {code}: expected {expected}, got [{string.Join("; ", entries)}]");
                }
            }
        }

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} metadata mismatches:\n" + string.Join("\n", mismatches));
    }

    private static string? CheckErrorContract(
        ProbeCase probeCase,
        WireResult wire)
    {
        var code = SweepStatus(probeCase.Mode)!.Value;
        var empty = probeCase.Mode.EndsWith("-empty", StringComparison.Ordinal);
        var problems = new List<string>();

        if (wire.Status != code)
        {
            problems.Add($"status {wire.Status}");
        }

        switch (probeCase.OperationId)
        {
            case "setAllNone" or "setAllFrameworkProblem":
                if (!string.Equals(wire.MediaType, "application/problem+json", StringComparison.Ordinal))
                {
                    problems.Add($"media {wire.MediaType}");
                }

                if (!TryReadStatus(wire.Body, out var bodyStatus) || bodyStatus != code)
                {
                    problems.Add("body is not a ProblemDetails with the response status");
                }

                if (!empty && !wire.Body.Contains(" handler", StringComparison.Ordinal))
                {
                    problems.Add("the handler's value is missing from the ProblemDetails");
                }

                break;
            case "setAllJsonProblem":
                ExpectDeclaredBody("application/json", $"\"detail\":\"{code} problem from handler\"", contains: true);
                break;
            case "setAllJsonString":
                ExpectDeclaredBody("application/json", $"\"{code} message from handler\"", contains: false);
                break;
            case "setAllTextPlain":
                ExpectDeclaredBody("text/plain", $"{code} message from handler", contains: false);
                break;
        }

        // TooManyRequests(value, retryAfterSeconds) sets Retry-After for every declaration; TooManyRequests() sets none.
        if (code == 429 && !string.Equals(wire.RetryAfter, empty ? null : "30", StringComparison.Ordinal))
        {
            problems.Add($"Retry-After '{wire.RetryAfter}'");
        }

        return problems.Count == 0
            ? null
            : $"{probeCase.Name}: {string.Join("; ", problems)} (got {wire.Status} {wire.ContentType} {Short(wire.Body)})";

        void ExpectDeclaredBody(
            string mediaType,
            string body,
            bool contains)
        {
            if (empty)
            {
                // The factory writes no body; the generated host wiring (AddProblemDetails +
                // UseStatusCodePages) fills it with a ProblemDetails.
                if (!string.Equals(wire.MediaType, "application/problem+json", StringComparison.Ordinal) ||
                    !TryReadStatus(wire.Body, out var emptyStatus) ||
                    emptyStatus != code)
                {
                    problems.Add("expected a ProblemDetails for the empty body");
                }

                return;
            }

            if (!string.Equals(wire.MediaType, mediaType, StringComparison.Ordinal))
            {
                problems.Add($"media {wire.MediaType}");
            }

            var matches = contains
                ? wire.Body.Contains(body, StringComparison.Ordinal)
                : string.Equals(wire.Body, body, StringComparison.Ordinal);
            if (!matches)
            {
                problems.Add("body");
            }
        }
    }

    private static bool TryReadStatus(
        string body,
        out int status)
    {
        status = 0;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("status", out var value) &&
                value.TryGetInt32(out status))
            {
                return true;
            }
        }
        catch (JsonException)
        {
            // Not JSON: not a ProblemDetails.
        }

        return false;
    }

    internal static List<(string HintName, string Source)> GenerateServer()
        => Generate(new ApiServerGenerator(), ".atc-rest-api-server", ServerMarker, includeMinimalApi: true);

    internal static List<(string HintName, string Source)> Generate(
        IIncrementalGenerator generator,
        string markerFileName,
        string markerContent,
        bool includeMinimalApi = false,
        string? yaml = null)
    {
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new CompilationVerificationHarness.InMemoryAdditionalText($"{ProjectName}.yaml", yaml ?? Yaml),
            new CompilationVerificationHarness.InMemoryAdditionalText(markerFileName, markerContent));

        var compilation = CSharpCompilation.Create(
            ProjectName,
            references: CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _, CancellationToken.None);

        return driver.GetRunResult().GeneratedTrees
            .Select(t => (t.FilePath, t.GetText().ToString()))
            .ToList();
    }

    private static string BuildYaml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            openapi: 3.0.3
            info:
              title: ErrorBodies
              version: 1.0.0
            paths:
            """);

        foreach (var operation in SetOperations)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $$"""
                  /{{operation.Path}}/{mode}:
                    put:
                      operationId: {{operation.OperationId}}
                      parameters:
                        - name: mode
                          in: path
                          required: true
                          schema:
                            type: string
                      requestBody:
                        $ref: '#/components/requestBodies/SetRequest'
                      responses:
                        '204':
                          description: No Content
                """);

            switch (operation.Path)
            {
                case "mixed":
                    // The motivating shape: a JSON string for 400, nothing declared for 404/422.
                    sb.AppendLine("""
                                '400':
                                  description: Bad Request
                                  content:
                                    application/json:
                                      schema:
                                        type: string
                                '404':
                                  description: Not Found
                                '422':
                                  description: Unprocessable Entity
                        """);
                    break;
                case "ref-response":
                    sb.AppendLine("""
                                '404':
                                  $ref: '#/components/responses/NotFound'
                                '422':
                                  $ref: '#/components/responses/NotFound'
                        """);
                    break;
                default:
                    var declared = operation.AllStatuses
                        ? AllErrorStatusCodes.Select(c => (c.ToString(CultureInfo.InvariantCulture), $"Status {c}")).ToArray()
                        : [("400", "Bad Request"), ("404", "Not Found"), ("409", "Conflict"), ("422", "Unprocessable Entity")];
                    foreach (var (code, description) in declared)
                    {
                        if (code == "409" && !operation.IncludeConflict)
                        {
                            continue;
                        }

                        sb.AppendLine(CultureInfo.InvariantCulture, $"""
                                    '{code}':
                                      description: {description}
                            """);
                        if (operation.ErrorContent is not null)
                        {
                            sb.Append(operation.ErrorContent);
                            sb.AppendLine();
                        }
                    }

                    break;
            }
        }

        // Every status below 400 that HttpStatusCode defines, one operation each: a JSON body where HTTP carries one,
        // a Location header for the redirects, nothing for the others.
        foreach (var code in NonErrorStatusCodes)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  /statuses/{code}/{{mode}}:");
            sb.AppendLine("    get:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"      operationId: getStatus{code}");
            sb.AppendLine("      parameters:");
            sb.AppendLine("        - name: mode");
            sb.AppendLine("          in: path");
            sb.AppendLine("          required: true");
            sb.AppendLine("          schema:");
            sb.AppendLine("            type: string");
            sb.AppendLine("      responses:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        '{code}':");
            sb.AppendLine(CultureInfo.InvariantCulture, $"          description: Status {code}");
            if (StatusCodesWithBody.Contains(code))
            {
                sb.AppendLine("          content:");
                sb.AppendLine("            application/json:");
                sb.AppendLine("              schema:");
                sb.AppendLine("                $ref: '#/components/schemas/Item'");
            }
            else if (RedirectStatusCodes.Contains(code))
            {
                sb.AppendLine("          headers:");
                sb.AppendLine("            Location:");
                sb.AppendLine("              schema:");
                sb.AppendLine("                type: string");
            }
        }

        sb.AppendLine("""
              /faults/{mode}:
                get:
                  operationId: getFault
                  parameters:
                    - name: mode
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Item'
                    '500':
                      description: Internal Server Error
                    '503':
                      description: Service Unavailable
              /default-error/{mode}:
                get:
                  operationId: getDefaultError
                  parameters:
                    - name: mode
                      in: path
                      required: true
                      schema:
                        type: string
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Item'
                    default:
                      description: Unexpected error
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/ErrorResponse'
            components:
              requestBodies:
                SetRequest:
                  required: true
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/SetRequest'
              responses:
                NotFound:
                  description: Referenced error response
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/RefOnlyError'
              schemas:
                RefOnlyError:
                  description: Referenced only through components/responses, so it is only generated when references are resolved.
                  type: object
                  properties:
                    code:
                      type: string
                    message:
                      type: string
                SetRequest:
                  type: object
                  required:
                    - reason
                  properties:
                    reason:
                      type: string
                      maxLength: 5
                Item:
                  type: object
                  properties:
                    name:
                      type: string
                ErrorResponse:
                  type: object
                  properties:
                    code:
                      type: string
                    message:
                      type: string
                ApiProblem:
                  type: object
                  properties:
                    type:
                      type: string
                    title:
                      type: string
                    status:
                      type: integer
                      format: int32
                    detail:
                      type: string
                    instance:
                      type: string
                ProblemDetails:
                  description: The RFC 9457 shape, under the name the generator binds to the framework type.
                  type: object
                  properties:
                    type:
                      type: string
                    title:
                      type: string
                    status:
                      type: integer
                      format: int32
                    detail:
                      type: string
                    instance:
                      type: string
            """);

        return sb.ToString();
    }

    /// <summary>
    /// Every case the harness drives through both the raw wire and the generated clients. Cases whose
    /// factory method the server generator did not emit are left out, because no handler can produce them.
    /// </summary>
    private static List<ProbeCase> BuildCases(
        IReadOnlyDictionary<string, Dictionary<string, Factory>> factories)
    {
        var cases = new List<ProbeCase>();
        foreach (var operation in SetOperations)
        {
            var available = factories[operation.OperationId];
            foreach (var mode in ModesFor(operation))
            {
                if (ResolveFactory(mode, available) is null && !mode.StartsWith("throw", StringComparison.Ordinal))
                {
                    // A declared status always has a factory: a missing one is a generator bug, not a case to skip.
                    if (SweepStatus(mode) is not null && !mode.EndsWith("-empty", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"The server generator wrote no factory for {operation.OperationId}/{mode}");
                    }

                    continue;
                }

                cases.Add(new ProbeCase(operation.OperationId, operation.Segment, mode, ValidReason, IsPut: true));
            }

            // The ValidationFilter rejects the body before the handler runs.
            cases.Add(new ProbeCase(operation.OperationId, operation.Segment, "ok", TooLongReason, IsPut: true));
        }

        cases.AddRange(FaultModes.Select(mode => new ProbeCase("getFault", "Faults", mode, Reason: null, IsPut: false)));

        // Every status below 400 - also one the generator writes no factory for (the case shows what is sent).
        cases.AddRange(NonErrorStatusCodes.Select(code => new ProbeCase($"getStatus{code}", "Statuses", $"s{code}", Reason: null, IsPut: false)));
        cases.AddRange(StubResponses.Keys.Select(mode => new ProbeCase("getFault", "Faults", mode, Reason: null, IsPut: false)));

        // The same bodies where the specification declares a model (ApiProblem) for every status.
        cases.AddRange(StubResponses.Keys.Select(mode => new ProbeCase("setAllJsonProblem", "AllJsonProblem", mode, ValidReason, IsPut: true)));
        cases.AddRange(DefaultErrorModes.Select(mode => new ProbeCase("getDefaultError", "DefaultError", mode, Reason: null, IsPut: false)));

        return cases;
    }

    private static string[] ModesFor(SetOperation operation)
        => operation.AllStatuses
            ? AllErrorStatusCodes
                .SelectMany(c => new[] { $"s{c}", $"s{c}-empty" })
                .Prepend("ok")
                .ToArray()
            : SetModes;

    /// <summary>
    /// The status code a sweep mode (<c>s404</c>, <c>s404-empty</c>) drives, or <see langword="null"/>.
    /// </summary>
    private static int? SweepStatus(string mode)
        => mode.StartsWith('s') &&
           int.TryParse(mode.AsSpan(1, 3), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
            ? code
            : null;

    private static (string Name, bool Empty)? ResolveFactory(
        string mode,
        Dictionary<string, Factory> available)
    {
        // Sweep modes: the generator names each factory after HttpStatusCodeHelper.ToEnumName.
        var sweepStatus = SweepStatus(mode);
        (string Name, bool Empty) wanted = sweepStatus is not null
            ? (sweepStatus == 200 ? "Ok" : HttpStatusCodeHelper.ToEnumName(sweepStatus.Value), mode.EndsWith("-empty", StringComparison.Ordinal))
            : mode switch
        {
            "ok" => (available.ContainsKey("NoContent") ? "NoContent" : "Ok", false),
            "bad" => ("BadRequest", false),
            "bad-empty" => ("BadRequest", true),
            "missing" => ("NotFound", false),
            "missing-empty" => ("NotFound", true),
            "conflict" => ("Conflict", false),
            "invalid" => ("UnprocessableEntity", false),
            "invalid-empty" => ("UnprocessableEntity", true),
            "error500" => ("InternalServerError", false),
            "error500-empty" => ("InternalServerError", true),
            "unavailable" => ("ServiceUnavailable", false),
            "error" => ("Error", false),
            _ => (string.Empty, false),
        };

        if (!available.TryGetValue(wanted.Name, out var factory))
        {
            return null;
        }

        // An "-empty" variant only means something for a factory that takes an optional argument.
        if (wanted.Empty && !factory.SupportsEmptyCall)
        {
            return null;
        }

        return wanted;
    }

    private static Dictionary<string, Dictionary<string, Factory>> ParseFactories(
        List<(string HintName, string Source)> serverSources)
    {
        var result = new Dictionary<string, Dictionary<string, Factory>>(StringComparer.Ordinal);
        var operations = SetOperations
            .Select(o => (o.OperationId, o.Segment))
            .Append(("getFault", "Faults"))
            .Append(("getDefaultError", "DefaultError"))
            .Concat(NonErrorStatusCodes.Select(code => ($"getStatus{code}", "Statuses")));

        foreach (var (operationId, segment) in operations)
        {
            var resultClass = char.ToUpperInvariant(operationId[0]) + operationId[1..] + "Result";
            var source = serverSources.Single(s => s.HintName.EndsWith($".{segment}.Results.g.cs", StringComparison.Ordinal)).Source;
            var factories = new Dictionary<string, Factory>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(source, $@"public static {resultClass} (?<name>\w+)\((?<params>[^)]*)\)", RegexOptions.None, TimeSpan.FromSeconds(1)))
            {
                var parameters = Regex.Replace(match.Groups["params"].Value, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();

                // A 429 value factory leads with the 2.x retryAfterSeconds parameter; the value follows it.
                var leadsWithRetryAfter = parameters.StartsWith("int? retryAfterSeconds", StringComparison.Ordinal) &&
                                          parameters.Contains(',', StringComparison.Ordinal);
                var valueParameter = leadsWithRetryAfter
                    ? parameters[(parameters.IndexOf(',', StringComparison.Ordinal) + 1)..].Trim()
                    : parameters;
                var parameterType = valueParameter.Length == 0
                    ? null
                    : valueParameter.Split(' ')[0].TrimEnd('?');
                var name = match.Groups["name"].Value;
                if (!factories.TryGetValue(name, out var factory))
                {
                    factory = new Factory([]);
                    factories[name] = factory;
                }

                factory.Overloads.Add(new FactoryOverload(
                    parameters,
                    parameterType,
                    ParameterIsOptional: valueParameter.Length == 0 || valueParameter.Contains("= null", StringComparison.Ordinal),
                    LeadsWithRetryAfter: leadsWithRetryAfter,
                    EndsWithRetryAfter: !leadsWithRetryAfter && parameters.Contains(", int? retryAfterSeconds", StringComparison.Ordinal)));
            }

            result[operationId] = factories;
        }

        return result;
    }

    private static string BuildOperationProbeSource(
        IReadOnlyList<ProbeCase> cases)
    {
        var sb = new StringBuilder();
        AppendProbeUsings(sb, cases, "Endpoints.Interfaces");
        sb.AppendLine("""
            namespace ErrorBodies.RuntimeTest;

            public static class Probe
            {
                public static async Task<List<string[]>> RunAsync(string baseUrl)
                {
                    var services = new ServiceCollection();
                    services.AddHttpClient(Constants.HttpClientName, c =>
                    {
                        c.BaseAddress = new Uri(baseUrl);
                        c.Timeout = TimeSpan.FromSeconds(10);
                    });
                    services.AddErrorBodiesEndpoints();
                    using var provider = services.BuildServiceProvider();
                    var rows = new List<string[]>();
            """);

        foreach (var probeCase in cases)
        {
            var pascal = char.ToUpperInvariant(probeCase.OperationId[0]) + probeCase.OperationId[1..];
            var parameters = probeCase.IsPut
                ? $"new {pascal}Parameters(\"{probeCase.Mode}\", new SetRequest(\"{probeCase.Reason}\"))"
                : $"new {pascal}Parameters(\"{probeCase.Mode}\")";
            sb.AppendLine(CultureInfo.InvariantCulture, $"        try {{ rows.Add(Describe(\"{probeCase.Name}\", await provider.GetRequiredService<I{pascal}Endpoint>().ExecuteAsync({parameters}))); }}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        catch (Exception ex) {{ rows.Add([\"{probeCase.Name}\", \"0\", \"False\", \"-\", $\"CALL THREW {{ex.GetType().Name}}: {{ex.Message}}\", string.Empty]); }}");
        }

        sb.AppendLine("""
                    return rows;
                }

                private static string[] Describe(string name, EndpointResponse response)
                {
                    var type = response.GetType();
                    var accessor = "(none)";
                    // IsDefaultError first: it is the most specific description where it applies (a 500
                    // that carries the spec's default body is both IsInternalServerError and IsDefaultError).
                    foreach (var isProperty in type.GetProperties().Where(p => p.PropertyType == typeof(bool) && p.Name.StartsWith("Is", StringComparison.Ordinal) && p.Name != "IsSuccess").OrderBy(p => p.Name == "IsDefaultError" ? 0 : 1))
                    {
                        if (!(bool)isProperty.GetValue(response)!)
                        {
                            continue;
                        }

                        var contentProperty = type.GetProperty(isProperty.Name[2..] + "Content");
                        if (contentProperty is null)
                        {
                            accessor = $"{isProperty.Name} (no content property)";
                            break;
                        }

                        try
                        {
                            accessor = $"{contentProperty.Name} => {Show(contentProperty.GetValue(response))}";
                        }
                        catch (System.Reflection.TargetInvocationException ex)
                        {
                            accessor = $"{contentProperty.Name} THROWS {ex.InnerException!.GetType().Name}: {ex.InnerException.Message}";
                        }

                        break;
                    }

                    // The field errors a model cannot hold (BadRequestValidationErrors), when the status has them.
                    var validationErrors = type.GetProperties()
                        .Where(p => p.Name.EndsWith("ValidationErrors", StringComparison.Ordinal))
                        .Select(p => p.GetValue(response))
                        .FirstOrDefault(v => v is not null);
                    var errors = validationErrors is null ? string.Empty : System.Text.Json.JsonSerializer.Serialize(validationErrors);

                    return [name, ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture), response.IsSuccess.ToString(), Show(response.ContentObject), accessor, response.Content, errors];
                }

                private static string Show(object? value)
                    => value switch
                    {
                        null => "null",
                        string s => $"string {System.Text.Json.JsonSerializer.Serialize(s)}",
                        _ => $"{value.GetType().Name} {System.Text.Json.JsonSerializer.Serialize(value, value.GetType())}",
                    };
            }
            """);

        return sb.ToString();
    }

    private static string BuildTypedProbeSource(
        IReadOnlyList<ProbeCase> cases,
        string resultStyle)
    {
        var sb = new StringBuilder();
        AppendProbeUsings(sb, cases, interfaceSuffix: null);
        sb.AppendLine("""
            namespace ErrorBodies.RuntimeTest;

            public static class Probe
            {
                public static async Task<List<string[]>> RunAsync(string baseUrl)
                {
                    using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };
                    var rows = new List<string[]>();
            """);

        foreach (var probeCase in cases)
        {
            var pascal = char.ToUpperInvariant(probeCase.OperationId[0]) + probeCase.OperationId[1..];
            var parameters = probeCase.IsPut
                ? $"new {pascal}Parameters(\"{probeCase.Mode}\", new SetRequest(\"{probeCase.Reason}\"))"
                : $"new {pascal}Parameters(\"{probeCase.Mode}\")";
            var call = $"new {probeCase.Segment}Client(http).{pascal}Async({parameters})";
            if (string.Equals(resultStyle, "Result", StringComparison.Ordinal))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        try {{ var r = await {call}; rows.Add([\"{probeCase.Name}\", $\"{{(int)r.StatusCode}} IsSuccess={{r.IsSuccess}} Content={{r.Content}} ContentObject={{r.ContentObject?.GetType().Name ?? \"null\"}}\"]); }}");
            }
            else
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        try {{ await {call}; rows.Add([\"{probeCase.Name}\", \"returned normally\"]); }}");
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"        catch (Exception ex) {{ rows.Add([\"{probeCase.Name}\", $\"{{ex.GetType().Name}}: {{ex.Message}}\"]); }}");
        }

        sb.AppendLine("""
                    return rows;
                }
            }
            """);

        return sb.ToString();
    }

    private static void AppendProbeUsings(
        StringBuilder sb,
        IReadOnlyList<ProbeCase> cases,
        string? interfaceSuffix)
    {
        sb.AppendLine("using Atc.Rest.Client;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using ErrorBodies.Generated;");
        sb.AppendLine("using ErrorBodies.Generated.Models;");
        foreach (var segment in cases.Select(c => c.Segment).Distinct(StringComparer.Ordinal))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"using ErrorBodies.Generated.{segment}.Client;");
            if (interfaceSuffix is not null)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"using ErrorBodies.Generated.{segment}.{interfaceSuffix};");
            }
        }

        sb.AppendLine();
    }

    private static GeneratedClient GenerateClientAssembly(
        string marker,
        Func<IReadOnlyList<ProbeCase>, string> probeSourceBuilder)
    {
        var serverFactories = ParseFactories(GenerateServer());
        var cases = BuildCases(serverFactories);

        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker);
        var registrations = sources
            .Select(s => (
                Name: Path.GetFileName(s.HintName),
                Lines: s.Source
                    .Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => l.StartsWith("responseBuilder.Add", StringComparison.Ordinal))
                    .Select(l => l
                        .Replace("responseBuilder.", string.Empty, StringComparison.Ordinal)
                        .Replace("HttpStatusCode.", string.Empty, StringComparison.Ordinal)
                        .TrimEnd(';'))
                    .ToList()))
            .Where(x => x.Lines.Count > 0)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => $"- `{x.Name}`: {string.Join(", ", x.Lines.Select(l => $"`{l}`"))}")
            .ToList();

        sources.Add(("Probe.cs", probeSourceBuilder(cases)));

        return new GeneratedClient(CompilationVerificationHarness.EmitAndLoad(sources), registrations);
    }

    private static Task<List<string[]>> RunProbeAsync(
        Assembly assembly,
        Uri baseAddress)
    {
        var probe = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!;
        return (Task<List<string[]>>)probe.GetMethod("RunAsync")!.Invoke(null, [baseAddress.ToString()])!;
    }

    private static string Cell(string? value)
        => (value ?? string.Empty)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("`", "'", StringComparison.Ordinal);

    private static void Dump(
        string directory,
        List<(string HintName, string Source)> sources)
    {
        Directory.CreateDirectory(directory);
        foreach (var (hintName, source) in sources)
        {
            File.WriteAllText(Path.Combine(directory, Path.GetFileName(hintName)), source);
        }
    }

    private sealed record SetOperation(
        string Path,
        string OperationId,
        string Segment,
        string? ErrorContent,
        bool IncludeConflict,
        bool AllStatuses = false);

    /// <summary>
    /// One generated factory method name with all its overloads (e.g. <c>NotFound(string?)</c> and
    /// <c>NotFound(ProblemDetails)</c>).
    /// </summary>
    private sealed record Factory(
        List<FactoryOverload> Overloads)
    {
        public string Signature
            => string.Join(" | ", Overloads.Select(o => o.Signature));

        /// <summary>
        /// The overload a handler calls to pass a value: the richest one (not <c>string</c>) when there is a choice.
        /// </summary>
        public FactoryOverload? ForArgument
            => Overloads.FirstOrDefault(o => o.ParameterType is not null and not "string")
               ?? Overloads.FirstOrDefault(o => o.ParameterType is not null);

        /// <summary>
        /// Whether <c>X()</c> is callable and means something different from the with-argument call.
        /// </summary>
        public bool SupportsEmptyCall
            => Overloads.Any(o => o.ParameterType is not null) &&
               Overloads.Any(o => o.ParameterType is null || o.ParameterIsOptional);
    }

    private sealed record FactoryOverload(
        string Signature,
        string? ParameterType,
        bool ParameterIsOptional,
        bool LeadsWithRetryAfter = false,
        bool EndsWithRetryAfter = false);

    internal sealed record ProbeCase(
        string OperationId,
        string Segment,
        string Mode,
        string? Reason,
        bool IsPut)
    {
        public string Name
            => Reason is null or ValidReason
                ? $"{OperationId}/{Mode}"
                : $"{OperationId}/{Mode} (reason too long)";
    }

    internal sealed record WireResult(
        string Case,
        int Status,
        string ContentType,
        string Body,
        string? RetryAfter = null,
        string? Location = null)
    {
        /// <summary>
        /// The media type without parameters (<c>application/problem+json</c>), or <c>(none)</c>.
        /// </summary>
        public string MediaType
            => ContentType.Split(';')[0].Trim();
    }

    private sealed record GeneratedClient(
        Assembly Assembly,
        List<string> Registrations);

    /// <summary>
    /// The generated server, with handlers derived from its own factory methods, hosted on Kestrel.
    /// </summary>
    private sealed class ErrorBodyHost : IAsyncDisposable
    {
        private readonly WebApplication app;
        private readonly HttpClientHandler handler;
        private readonly HttpClient client;

        private ErrorBodyHost(
            WebApplication app,
            Dictionary<string, Dictionary<string, Factory>> factories,
            List<string> producesMetadata)
        {
            this.app = app;
            Factories = factories;
            ProducesMetadata = producesMetadata;
            BaseAddress = new Uri(app.Urls.First());

            // The wire as sent: a redirect is recorded, not followed.
            handler = new HttpClientHandler { AllowAutoRedirect = false, CheckCertificateRevocationList = true };
            client = new HttpClient(handler) { BaseAddress = BaseAddress };
            Cases = BuildCases(factories);
        }

        public Uri BaseAddress { get; }

        public Dictionary<string, Dictionary<string, Factory>> Factories { get; }

        public List<string> ProducesMetadata { get; }

        public List<ProbeCase> Cases { get; }

        /// <summary>
        /// Generates, compiles and starts the server through the generated <c>Add/Map{Project}Api</c>.
        /// </summary>
        /// <param name="problemDetailsWiringOff">
        /// Turn the generated ProblemDetails host wiring off through the generated options
        /// (<c>ApiServiceOptions.UseProblemDetails</c>, <c>ApiMiddlewareOptions.UseStatusCodePages</c>).
        /// </param>
        public static async Task<ErrorBodyHost> StartAsync(
            bool problemDetailsWiringOff = false)
        {
            var sources = GenerateServer();
            var factories = ParseFactories(sources);
            var producesMetadata = sources
                .Where(s => s.HintName.Contains("EndpointDefinition", StringComparison.Ordinal))
                .SelectMany(s => Regex.Matches(s.Source, @"\.WithName\(""(?<name>\w+)""\)(?<produces>(\s*\.(Produces|ProducesProblem|ProducesValidationProblem)[^\r\n]*)*)", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)))
                .Select(m => $"- `{m.Groups["name"].Value}`: {string.Join(" ", m.Groups["produces"].Value.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Select(l => $"`{l}`"))}")
                .OrderBy(l => l, StringComparer.Ordinal)
                .ToList();

            sources.Add(("RuntimeTestHandlers.cs", BuildHandlerSource(factories, sources)));
            if (problemDetailsWiringOff)
            {
                sources.Add(("RuntimeTestOptions.cs", """
                    namespace ErrorBodies.RuntimeTest;

                    public static class RuntimeTestOptions
                    {
                        public static void ServiceOptionsOff(ErrorBodies.Generated.ApiServiceOptions options)
                            => options.UseProblemDetails = false;

                        public static void MiddlewareOptionsOff(ErrorBodies.Generated.ApiMiddlewareOptions options)
                            => options.UseStatusCodePages = false;
                    }
                    """));
            }

            var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
            var serviceExtensions = assembly.GetType("ErrorBodies.Generated.Extensions.UnifiedServiceCollectionExtensions", throwOnError: true)!;
            var appExtensions = assembly.GetType("ErrorBodies.Generated.Extensions.UnifiedWebApplicationExtensions", throwOnError: true)!;
            var registration = assembly.GetType("ErrorBodies.RuntimeTest.RuntimeTestRegistration", throwOnError: true)!;

            object? serviceOptions = null;
            object? middlewareOptions = null;
            if (problemDetailsWiringOff)
            {
                var options = assembly.GetType("ErrorBodies.RuntimeTest.RuntimeTestOptions", throwOnError: true)!;
                serviceOptions = ToAction(options.GetMethod("ServiceOptionsOff")!);
                middlewareOptions = ToAction(options.GetMethod("MiddlewareOptionsOff")!);
            }

            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();

            registration.GetMethod("Register")!.Invoke(null, [builder.Services]);
            serviceExtensions
                .GetMethod("AddErrorBodiesApi")!
                .Invoke(null, [builder.Services, serviceOptions]);

            var webApp = builder.Build();

            // Bodies the generated server never writes (any route ending in a stub mode), in front of the generated endpoints.
            webApp.Use(async (context, next) =>
            {
                var lastSegment = context.Request.Path.Value?.Split('/')[^1] ?? string.Empty;
                if (lastSegment.StartsWith("stub-", StringComparison.Ordinal) &&
                    StubResponses.TryGetValue(lastSegment, out var stub))
                {
                    context.Response.StatusCode = stub.Status;
                    if (stub.ContentType is not null)
                    {
                        context.Response.ContentType = stub.ContentType;
                    }

                    await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(stub.Body), context.RequestAborted);
                    return;
                }

                await next(context);
            });
            webApp.Urls.Add("http://127.0.0.1:0");

            appExtensions
                .GetMethod("MapErrorBodiesApi")!
                .Invoke(null, [webApp, middlewareOptions]);

            await webApp.StartAsync(TestContext.Current.CancellationToken);

            return new ErrorBodyHost(webApp, factories, producesMetadata);

            static Delegate ToAction(MethodInfo method)
                => Delegate.CreateDelegate(
                    typeof(Action<>).MakeGenericType(method.GetParameters()[0].ParameterType),
                    method);
        }

        /// <summary>
        /// The response metadata the running host actually registered per endpoint - what any
        /// OpenAPI document generator (Microsoft.AspNetCore.OpenApi, Swashbuckle) reads.
        /// </summary>
        public List<string> DescribeEndpointResponseMetadata()
            => app.Services
                .GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>()
                .Endpoints
                .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>() is not null)
                .Select(e => (
                    Name: e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()!.EndpointName,
                    Responses: e.Metadata
                        .GetOrderedMetadata<Microsoft.AspNetCore.Http.Metadata.IProducesResponseTypeMetadata>()
                        .Select(m => $"`{m.StatusCode} {m.Type?.Name ?? "(no type)"} [{string.Join(", ", m.ContentTypes)}]`")))
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .Select(x => $"- `{x.Name}`: {string.Join(" ", x.Responses)}")
                .ToList();

        /// <summary>
        /// The status codes in the response metadata the running host registered for one endpoint.
        /// </summary>
        public List<int> ResponseStatusCodes(string endpointName)
            => ResponseMetadata(endpointName)
                .Select(m => m.StatusCode)
                .ToList();

        /// <summary>
        /// The response metadata the running host registered for one endpoint: status, body type name and
        /// content types.
        /// </summary>
        public List<(int StatusCode, string TypeName, string ContentTypes)> ResponseMetadata(
            string endpointName)
            => app.Services
                .GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>()
                .Endpoints
                .Single(e => string.Equals(
                    e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()?.EndpointName,
                    endpointName,
                    StringComparison.Ordinal))
                .Metadata
                .GetOrderedMetadata<Microsoft.AspNetCore.Http.Metadata.IProducesResponseTypeMetadata>()
                .Select(m => (m.StatusCode, m.Type?.Name ?? "(none)", string.Join(",", m.ContentTypes)))
                .ToList();

        public async Task<List<WireResult>> CaptureWireAsync()
        {
            var results = new List<WireResult>();
            foreach (var probeCase in Cases)
            {
                results.Add(await SendCaseAsync(probeCase));
            }

            // Failures raised by the framework before any generated code runs.
            using (var malformed = new HttpRequestMessage(HttpMethod.Put, "/no-content/ok"))
            {
                malformed.Content = new StringContent("{", Encoding.UTF8, "application/json");
                results.Add(await SendAsync("framework: malformed JSON body", malformed));
            }

            using (var missingBody = new HttpRequestMessage(HttpMethod.Put, "/no-content/ok"))
            {
                results.Add(await SendAsync("framework: missing required body", missingBody));
            }

            using (var unknownRoute = new HttpRequestMessage(HttpMethod.Get, "/does-not-exist"))
            {
                results.Add(await SendAsync("framework: unknown route", unknownRoute));
            }

            using (var wrongMethod = new HttpRequestMessage(HttpMethod.Get, "/no-content/ok"))
            {
                results.Add(await SendAsync("framework: wrong method", wrongMethod));
            }

            return results;
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            handler.Dispose();
            await app.StopAsync(TestContext.Current.CancellationToken);
            await app.DisposeAsync();
        }

        private static string BuildHandlerSource(
            Dictionary<string, Dictionary<string, Factory>> factories,
            List<(string HintName, string Source)> serverSources)
        {
            var modelNamespaces = serverSources
                .SelectMany(s => Regex.Matches(s.Source, @"^namespace (?<ns>[\w\.]+\.Models);", RegexOptions.Multiline, TimeSpan.FromSeconds(1)))
                .Select(m => m.Groups["ns"].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal);

            var sb = new StringBuilder();
            sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            foreach (var ns in modelNamespaces)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"using {ns};");
            }

            sb.AppendLine();
            sb.AppendLine("namespace ErrorBodies.RuntimeTest;");
            sb.AppendLine();

            var registrations = new List<string>();
            var operations = SetOperations
                .Select(o => (o.OperationId, o.Segment, Modes: ModesFor(o)))
                .Append(("getFault", "Faults", FaultModes))
                .Concat(NonErrorStatusCodes.Select(code => ($"getStatus{code}", "Statuses", (string[])[$"s{code}"])))
                .Append(("getDefaultError", "DefaultError", DefaultErrorModes));

            foreach (var (operationId, segment, modes) in operations)
            {
                var pascal = char.ToUpperInvariant(operationId[0]) + operationId[1..];
                var resultType = $"ErrorBodies.Generated.{segment}.Results.{pascal}Result";
                var handlerInterface = $"ErrorBodies.Generated.{segment}.Handlers.I{pascal}Handler";
                var parametersType = $"ErrorBodies.Generated.{segment}.Parameters.{pascal}Parameters";
                var available = factories[operationId];

                sb.AppendLine(CultureInfo.InvariantCulture, $"public sealed class {pascal}HandlerImpl : {handlerInterface}");
                sb.AppendLine("{");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    public System.Threading.Tasks.Task<{resultType}> ExecuteAsync({parametersType} parameters, System.Threading.CancellationToken cancellationToken = default)");
                sb.AppendLine(CultureInfo.InvariantCulture, $"        => System.Threading.Tasks.Task.FromResult<{resultType}>(parameters.Mode switch");
                sb.AppendLine("        {");
                foreach (var mode in modes)
                {
                    var expression = mode switch
                    {
                        "throw" => "throw new System.IO.InvalidDataException(\"boom from handler\")",
                        "throw-argument" => "throw new System.ArgumentException(\"argument rejected by handler\")",
                        "throw-invalid-operation" => "throw new System.InvalidOperationException(\"invalid operation in handler\")",
                        "throw-timeout" => "throw new System.TimeoutException(\"handler timed out\")",
                        "throw-canceled" => "throw new System.OperationCanceledException(\"canceled in handler\")",
                        "throw-unauthorized-access" => "throw new System.UnauthorizedAccessException(\"access denied in handler\")",
                        "throw-argument-null" => "throw new System.ArgumentNullException(\"reason\", \"null argument in handler\")",
                        "throw-bad-http-request" => "throw new Microsoft.AspNetCore.Http.BadHttpRequestException(\"payload too large in handler\", 413)",
                        "throw-not-implemented" => "throw new System.NotImplementedException(\"not implemented in handler\")",
                        _ when operationId == "getDefaultError" && mode.StartsWith('d') => $"{resultType}.Error(new ErrorBodies.Generated.Models.ErrorResponse(\"c{mode[1..]}\", \"{mode[1..]} default from handler\"), {mode[1..]})",
                        _ => BuildFactoryCall(resultType, mode, available),
                    };

                    if (expression is not null)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"            \"{mode}\" => {expression},");
                    }
                }

                sb.AppendLine("            _ => throw new System.NotSupportedException(parameters.Mode),");
                sb.AppendLine("        });");
                sb.AppendLine("}");
                sb.AppendLine();

                registrations.Add($"        services.AddScoped<{handlerInterface}, {pascal}HandlerImpl>();");
            }

            sb.AppendLine("public static class RuntimeTestRegistration");
            sb.AppendLine("{");
            sb.AppendLine("    public static void Register(IServiceCollection services)");
            sb.AppendLine("    {");
            foreach (var registration in registrations)
            {
                sb.AppendLine(registration);
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string? BuildFactoryCall(
            string resultType,
            string mode,
            Dictionary<string, Factory> available)
        {
            var resolved = ResolveFactory(mode, available);
            if (resolved is null)
            {
                return null;
            }

            var (name, empty) = resolved.Value;
            var overload = available[name].ForArgument;
            if (empty || overload is null)
            {
                return $"{resultType}.{name}()";
            }

            var status = SweepStatus(mode) ?? name switch
            {
                "BadRequest" => 400,
                "NotFound" => 404,
                "Conflict" => 409,
                "UnprocessableEntity" => 422,
                "InternalServerError" => 500,
                "ServiceUnavailable" => 503,
                _ => 500,
            };

            var argument = overload.ParameterType switch
            {
                "System.Uri" => $"new System.Uri(\"{RedirectTarget}\", System.UriKind.Relative)",
                "string" => $"\"{status} message from handler\"",
                "ValidationProblemDetails" => "new ValidationProblemDetails(new System.Collections.Generic.Dictionary<string, string[]> { [\"reason\"] = [\"reason rejected by handler\"] })",
                "ProblemDetails" => $"new ProblemDetails {{ Title = \"{name}\", Status = {status}, Detail = \"{status} problem from handler\" }}",
                "ErrorResponse" => $"new ErrorResponse(\"E{status}\", \"{status} error from handler\")",
                "ApiProblem" => $"new ApiProblem(\"about:blank\", \"{name}\", {status}, \"{status} problem from handler\", \"/instance\")",
                "Item" => "new Item(\"thing\")",
                "int" => "30",

                // Any other (e.g. generated inline) model with code/message properties.
                var modelType => $"System.Text.Json.JsonSerializer.Deserialize<{modelType}>(\"{{\\\"code\\\":\\\"E{status}\\\",\\\"message\\\":\\\"{status} error from handler\\\"}}\", new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!",
            };

            return overload switch
            {
                { LeadsWithRetryAfter: true } => $"{resultType}.{name}(30, {argument})",
                { EndsWithRetryAfter: true } => $"{resultType}.{name}({argument}, 30)",
                _ => $"{resultType}.{name}({argument})",
            };
        }

        private async Task<WireResult> SendCaseAsync(ProbeCase probeCase)
        {
            using var request = new HttpRequestMessage(
                probeCase.IsPut ? HttpMethod.Put : HttpMethod.Get,
                $"/{PathOf(probeCase)}/{probeCase.Mode}");
            if (probeCase.IsPut)
            {
                request.Content = JsonContent.Create(new { reason = probeCase.Reason });
            }

            return await SendAsync(probeCase.Name, request);
        }

        private static string PathOf(ProbeCase probeCase)
            => probeCase.IsPut
                ? SetOperations.Single(o => o.OperationId == probeCase.OperationId).Path
                : probeCase.OperationId switch
                {
                    "getFault" => "faults",
                    { } id when id.StartsWith("getStatus", StringComparison.Ordinal) => $"statuses/{id["getStatus".Length..]}",
                    _ => "default-error",
                };

        private async Task<WireResult> SendAsync(
            string name,
            HttpRequestMessage request)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                using var response = await client.SendAsync(request, timeout.Token);
                var body = await response.Content.ReadAsStringAsync(timeout.Token);
                return new WireResult(
                    name,
                    (int)response.StatusCode,
                    response.Content.Headers.ContentType?.ToString() ?? "(none)",
                    body.Length == 0 ? "(empty)" : body,
                    response.Headers.RetryAfter?.ToString(),
                    response.Headers.Location?.ToString());
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
            {
                // No final response (e.g. a 1xx status written as the response): recorded, not thrown.
                return new WireResult(name, 0, "(none)", $"(no response: {ex.GetType().Name}: {ex.Message})");
            }
        }
    }
}