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
public class ErrorResponseBodyRuntimeTests
{
    private const string ProjectName = "ErrorBodies";
    private const string ValidReason = "ok";
    private const string TooLongReason = "far-too-long";

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
    private static readonly int[] AllErrorStatusCodes =
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

    private static readonly string[] DefaultErrorModes =
    [
        "ok",
        "error",
        "throw",
    ];

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
                ("setAllNone", 400) => "ValidationProblemDetails ",
                ("setAllNone" or "setAllFrameworkProblem", not null) => "ProblemDetails ",
                ("setAllJsonProblem", not null) => "ApiProblem ",
                ("setAllJsonString" or "setAllTextPlain", not null) when withValue => $"string \"{code} message from handler\"",
                ("setAllJsonString" or "setAllTextPlain", not null) => "string ",
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
            .Where(r => r[0].StartsWith("getFault/", StringComparison.Ordinal) && !r[0].EndsWith("/ok", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(getFailures);
        Assert.All(getFailures, r =>
        {
            Assert.StartsWith("HttpRequestException: HTTP ", r[1], StringComparison.Ordinal);
            Assert.Contains("\"status\":", r[1], StringComparison.Ordinal);
        });
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

                if (!empty && code != 429 && !wire.Body.Contains(" handler", StringComparison.Ordinal))
                {
                    problems.Add("the handler's value is missing from the ProblemDetails");
                }

                if (!empty && code == 429 && !string.Equals(wire.RetryAfter, "30", StringComparison.Ordinal))
                {
                    problems.Add($"Retry-After '{wire.RetryAfter}'");
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
                    continue;
                }

                cases.Add(new ProbeCase(operation.OperationId, operation.Segment, mode, ValidReason, IsPut: true));
            }

            // The ValidationFilter rejects the body before the handler runs.
            cases.Add(new ProbeCase(operation.OperationId, operation.Segment, "ok", TooLongReason, IsPut: true));
        }

        cases.AddRange(FaultModes.Select(mode => new ProbeCase("getFault", "Faults", mode, Reason: null, IsPut: false)));
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
            ? (HttpStatusCodeHelper.ToEnumName(sweepStatus.Value), mode.EndsWith("-empty", StringComparison.Ordinal))
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
            .Append(("getDefaultError", "DefaultError"));

        foreach (var (operationId, segment) in operations)
        {
            var resultClass = char.ToUpperInvariant(operationId[0]) + operationId[1..] + "Result";
            var source = serverSources.Single(s => s.HintName.EndsWith($".{segment}.Results.g.cs", StringComparison.Ordinal)).Source;
            var factories = new Dictionary<string, Factory>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(source, $@"public static {resultClass} (?<name>\w+)\((?<params>[^)]*)\)", RegexOptions.None, TimeSpan.FromSeconds(1)))
            {
                var parameters = match.Groups["params"].Value.Trim();
                var parameterType = parameters.Length == 0
                    ? null
                    : parameters.Split(' ')[0].TrimEnd('?');
                var name = match.Groups["name"].Value;
                if (!factories.TryGetValue(name, out var factory))
                {
                    factory = new Factory([]);
                    factories[name] = factory;
                }

                factory.Overloads.Add(new FactoryOverload(
                    parameters,
                    parameterType,
                    ParameterIsOptional: parameters.Length == 0 || parameters.Contains("= null", StringComparison.Ordinal)));
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
                    services.AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri(baseUrl));
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
            sb.AppendLine(CultureInfo.InvariantCulture, $"        rows.Add(Describe(\"{probeCase.Name}\", await provider.GetRequiredService<I{pascal}Endpoint>().ExecuteAsync({parameters})));");
        }

        sb.AppendLine("""
                    return rows;
                }

                private static string[] Describe(string name, EndpointResponse response)
                {
                    var type = response.GetType();
                    var accessor = "(none)";
                    foreach (var isProperty in type.GetProperties().Where(p => p.PropertyType == typeof(bool) && p.Name.StartsWith("Is", StringComparison.Ordinal) && p.Name != "IsSuccess"))
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

                    return [name, ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture), response.IsSuccess.ToString(), Show(response.ContentObject), accessor];
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
                    using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
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
        bool ParameterIsOptional);

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
        string? RetryAfter = null)
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
            client = new HttpClient { BaseAddress = BaseAddress };
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
                var path = probeCase.IsPut
                    ? SetOperations.Single(o => o.OperationId == probeCase.OperationId).Path
                    : probeCase.OperationId == "getFault" ? "faults" : "default-error";
                using var request = new HttpRequestMessage(
                    probeCase.IsPut ? HttpMethod.Put : HttpMethod.Get,
                    $"/{path}/{probeCase.Mode}");
                if (probeCase.IsPut)
                {
                    request.Content = JsonContent.Create(new { reason = probeCase.Reason });
                }

                results.Add(await SendAsync(probeCase.Name, request));
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
                        "throw-not-implemented" => "throw new System.NotImplementedException(\"not implemented in handler\")",
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

            return $"{resultType}.{name}({argument})";
        }

        private async Task<WireResult> SendAsync(
            string name,
            HttpRequestMessage request)
        {
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            return new WireResult(
                name,
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.ToString() ?? "(none)",
                body.Length == 0 ? "(empty)" : body,
                response.Headers.RetryAfter?.ToString());
        }
    }
}