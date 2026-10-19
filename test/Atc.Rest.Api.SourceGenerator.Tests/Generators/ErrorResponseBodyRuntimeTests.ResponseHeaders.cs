namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The headers a response declares are what the handler's result can write: each is an optional, typed parameter of
/// the response's factory - after the parameters the factory has, so no call that compiled changes its meaning - and
/// is written with the result. A header the handler does not give is not written (a middleware may), and then the
/// result is the framework's own, unwrapped.
/// </summary>
public sealed partial class ErrorResponseBodyRuntimeTests
{
    private const string ResponseHeadersYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things:
            post:
              operationId: createThing
              responses:
                '201':
                  description: Created
                  headers:
                    Location:
                      schema:
                        type: string
                        format: uri
                    X-Request-Id:
                      $ref: '#/components/headers/RequestId'
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
          /things/{id}:
            get:
              operationId: getThing
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: The thing
                  headers:
                    ETag:
                      required: true
                      schema:
                        type: string
                    X-Rate-Limit:
                      schema:
                        type: integer
                        format: int32
                    X-Expires-After:
                      schema:
                        type: string
                        format: date-time
                    Last-Modified:
                      schema:
                        type: string
                        format: date-time
                    X-Tags:
                      schema:
                        type: array
                        items:
                          type: string
                    X-Flag:
                      schema:
                        type: boolean
                    Content-Type:
                      schema:
                        type: string
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
                '404':
                  description: Not found
                  headers:
                    Message:
                      schema:
                        type: string
                '429':
                  description: Slow down
                  headers:
                    Retry-After:
                      schema:
                        type: integer
                    X-RateLimit-Remaining:
                      schema:
                        type: integer
                        format: int64
            head:
              operationId: probeThing
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: The thing exists
                  headers:
                    ETag:
                      schema:
                        type: string
          /things/{id}/status:
            get:
              operationId: getStatus
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: The status
                  headers:
                    X-Mode:
                      schema:
                        $ref: '#/components/schemas/Mode'
                    X-Modes:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Mode'
                    X-Priority:
                      schema:
                        $ref: '#/components/schemas/Priority'
                    X-Ratio:
                      schema:
                        type: number
                        format: float
                    X-Level:
                      schema:
                        type: string
                        enum: [low, very-high]
                    X-Levels:
                      schema:
                        type: array
                        items:
                          type: string
                          enum: [low, very-high]
                    X-Window:
                      schema:
                        $ref: '#/components/schemas/Window'
                    X-Window-Exploded:
                      explode: true
                      schema:
                        $ref: '#/components/schemas/Window'
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
                '503':
                  description: Down for maintenance
                  headers:
                    X-Mode:
                      schema:
                        $ref: '#/components/schemas/Mode'
        components:
          headers:
            RequestId:
              schema:
                type: string
                format: uuid
          schemas:
            Thing:
              type: object
              required: [id]
              properties:
                id:
                  type: string
            Mode:
              type: string
              enum: [fast, slow-and-steady]
            Priority:
              type: integer
              enum: [1, 2, 3]
            Window:
              type: object
              required: [start]
              properties:
                start:
                  type: integer
                end:
                  type: integer
                mode:
                  $ref: '#/components/schemas/Mode'
        """;

    private const string ResponseHeadersProbe = """"
        using ErrorBodies.Generated.Models;
        using ErrorBodies.Generated.Results;
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
            // What a handler's unit test reads from a result without executing it: the status code and the value, through
            // the framework's interfaces - whether or not the result carries headers
            public static List<string> WhatATestSees()
            {
                var thing = new Thing("1");
                return
                [
                    Describe("ok", GetThingResult.Ok(thing)),
                    Describe("ok-with-header", GetThingResult.Ok(thing, eTag: "\"v1\"")),
                    Describe("notfound", GetThingResult.NotFound("gone")),
                    Describe("notfound-with-header", GetThingResult.NotFound("gone", messageHeader: "why")),
                    Describe("head-with-header", ProbeThingResult.Ok(eTag: "\"v2\"")),
                    Describe("created-with-location", CreateThingResult.Created(thing, location: "/things/1")),
                    Describe("created-with-location-and-header", CreateThingResult.Created(thing, location: "/things/1", xRequestId: new Guid("11111111-2222-3333-4444-555555555555"))),
                ];

                static string Describe(string name, IResult result)
                {
                    var inner = (IResult)result.GetType().GetProperty("Result")!.GetValue(result)!;
                    var status = inner is IStatusCodeHttpResult withStatus ? withStatus.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
                    var value = inner is IValueHttpResult withValue ? withValue.Value : null;

                    // A wrapper also gives what it wraps (INestedHttpResult), as the framework's Results<T1, T2> does
                    var wraps = string.Empty;
                    for (var nested = inner; nested is INestedHttpResult wrapper; nested = wrapper.Result)
                    {
                        wraps += " > " + wrapper.Result.GetType().Name.Split('`')[0];
                    }

                    var text = value switch
                    {
                        null => "(none)",
                        Thing t => "Thing " + t.Id,
                        Microsoft.AspNetCore.Mvc.ProblemDetails problem => "ProblemDetails " + problem.Detail,
                        _ => value.GetType().Name,
                    };
                    return $"{name}: status={status ?? "(none)"} value={text}{wraps}";
                }
            }

            public static async Task<List<string>> RunAsync()
            {
                var services = new ServiceCollection();
                services.AddLogging();
                services.ConfigureHttpJsonOptions(_ => { });
                using var provider = services.BuildServiceProvider();

                var thing = new Thing("1");
                var at = new DateTimeOffset(2026, 10, 13, 8, 30, 0, TimeSpan.FromHours(2));
                var lines = new List<string>();

                await Add("ok-all", GetThingResult.Ok(thing, eTag: "\"v1\"", xRateLimit: 5, xExpiresAfter: at, lastModified: at, xTags: new[] { "a", "b" }, xFlag: true));
                await Add("ok-some", GetThingResult.Ok(thing, xFlag: false));
                await Add("ok-none", GetThingResult.Ok(thing));
                await Add("notfound-positional", GetThingResult.NotFound("gone"));
                await Add("notfound-header", GetThingResult.NotFound("gone", messageHeader: "why"));
                await Add("toomany", GetThingResult.TooManyRequests(xRateLimitRemaining: 0));
                await Add("head", ProbeThingResult.Ok(eTag: "\"v2\""));
                await Add("created", CreateThingResult.Created(thing, xRequestId: new Guid("11111111-2222-3333-4444-555555555555")));
                await Add("status-all", GetStatusResult.Ok(thing, xMode: Mode.SlowAndSteady, xModes: new[] { Mode.Fast, Mode.SlowAndSteady }, xPriority: Priority.Value2, xRatio: 0.5f, xLevel: GetStatusXLevelHeader.VeryHigh, xLevels: new[] { GetStatusXLevelsHeader.Low, GetStatusXLevelsHeader.VeryHigh }, xWindow: new Window(Start: 1, End: 5, Mode: Mode.Fast), xWindowExploded: new Window(Start: 2)));
                await Add("status-unavailable", GetStatusResult.ServiceUnavailable(xMode: Mode.Fast));
                return lines;

                async Task Add(string name, IResult result)
                {
                    var context = new DefaultHttpContext { RequestServices = provider };
                    context.Response.Body = new MemoryStream();
                    await result.ExecuteAsync(context);
                    var headers = context.Response.Headers
                        .Where(h => !h.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(h => h.Key, StringComparer.Ordinal)
                        .Select(h => $"{h.Key}={h.Value}");
                    var inner = result.GetType().GetProperty("Result")!.GetValue(result)!.GetType().Name;
                    lines.Add($"{name}: {context.Response.StatusCode} [{string.Join("; ", headers)}] wrapped={inner == "ResponseHeadersHttpResult"}");
                }
            }
        }
        """";

    [Fact]
    public async Task Server_ResultFactories_WriteTheHeadersTheResponseDeclares()
    {
        var sources = Generate(new ApiServerGenerator(), ".atc-rest-api-server", ServerMarker, includeMinimalApi: true, ResponseHeadersYaml);
        sources.Add(("Probe.cs", ResponseHeadersProbe));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources, includeMinimalApi: true);
        var run = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("RunAsync")!;

        var lines = await (Task<List<string>>)run.Invoke(null, [])!;

        // A handler's unit test asserts the status code and the value through IStatusCodeHttpResult and IValueHttpResult:
        // a result that also carries headers answers like the one it wraps
        var seen = (List<string>)assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("WhatATestSees")!.Invoke(null, [])!;
        Assert.Equal(
            [
                "ok: status=200 value=Thing 1",
                "ok-with-header: status=200 value=Thing 1 > Ok",
                "notfound: status=404 value=ProblemDetails gone",
                "notfound-with-header: status=404 value=ProblemDetails gone > ProblemHttpResult",
                "head-with-header: status=200 value=(none) > Ok",
                "created-with-location: status=201 value=Thing 1 > Created",
                "created-with-location-and-header: status=201 value=Thing 1 > LocationHttpResult > Created",
            ],
            seen);

        // RFC 3339 for a date-time, the HTTP date for the headers HTTP defines as one, an array joined by commas
        Assert.Equal(
            [
                "ok-all: 200 [ETag=\"v1\"; Last-Modified=Tue, 13 Oct 2026 06:30:00 GMT; X-Expires-After=2026-10-13T08:30:00.0000000+02:00; X-Flag=true; X-Rate-Limit=5; X-Tags=a,b] wrapped=True",
                "ok-some: 200 [X-Flag=false] wrapped=True",
                "ok-none: 200 [] wrapped=False",
                "notfound-positional: 404 [] wrapped=False",
                "notfound-header: 404 [Message=why] wrapped=True",
                "toomany: 429 [X-RateLimit-Remaining=0] wrapped=True",
                "head: 200 [ETag=\"v2\"] wrapped=True",
                "created: 201 [X-Request-Id=11111111-2222-3333-4444-555555555555] wrapped=True",
                "status-all: 200 [X-Level=very-high; X-Levels=low,very-high; X-Mode=slow-and-steady; X-Modes=fast,slow-and-steady; X-Priority=2; X-Ratio=0.5; X-Window=start,1,end,5,mode,fast; X-Window-Exploded=start=2] wrapped=True",
                "status-unavailable: 503 [X-Mode=fast] wrapped=True",
            ],
            lines);

        // What a factory does not take: the body's own header, and a header it already writes from a parameter it has
        var getThing = assembly.GetTypes().Single(t => t.Name == "GetThingResult");
        Assert.DoesNotContain(getThing.GetMethod("Ok")!.GetParameters(), p => p.Name!.Contains("contentType", StringComparison.OrdinalIgnoreCase));
        Assert.Single(getThing.GetMethod("TooManyRequests")!.GetParameters(), p => p.Name!.StartsWith("retryAfter", StringComparison.Ordinal));
        var created = assembly.GetTypes().Single(t => t.Name == "CreateThingResult").GetMethods().Where(m => m.Name == "Created");
        Assert.All(created, m => Assert.True(m.GetParameters().Count(p => p.Name is "location" or "uri") <= 1));

        // Every header parameter is optional: a header the specification requires can still be a middleware's to write
        Assert.All(
            getThing.GetMethod("Ok")!.GetParameters().Where(p => p.Name != "response"),
            p => Assert.True(p.HasDefaultValue, p.Name));
    }

    private const string ResponseHeadersClientProbe = """"
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated;
        using ErrorBodies.Generated.Client;
        using ErrorBodies.Generated.Endpoints.Interfaces;

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

                var lines = new List<string>();
                foreach (var id in new[] { "all", "none", "odd" })
                {
                    var result = await provider.GetRequiredService<IGetThingEndpoint>().ExecuteAsync(new GetThingParameters(id));
                    lines.Add($"{id}: {(int)result.StatusCode} ETag={Show(result.ETagHeader)} X-Rate-Limit={Show(result.XRateLimitHeader)} X-Expires-After={Show(result.XExpiresAfterHeader?.ToString("O"))} Last-Modified={Show(result.LastModifiedHeader?.ToString("O"))} X-Tags={Show(result.XTagsHeader is null ? null : string.Join("|", result.XTagsHeader))} X-Flag={Show(result.XFlagHeader)} Message={Show(result.MessageHeader)} X-RateLimit-Remaining={Show(result.XRateLimitRemainingHeader)}");
                }

                var probe = await provider.GetRequiredService<IProbeThingEndpoint>().ExecuteAsync(new ProbeThingParameters("all"));
                lines.Add($"head: {(int)probe.StatusCode} ETag={Show(probe.ETagHeader)}");
                return lines;
            }

            private static string Show(object? value)
                => value is null ? "(null)" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;

            // "all": every declared header, as a server writes them. "none": no header. "odd": values that are not what
            // the specification says (a rate limit that is no number, a date that is no date) and a missing thing.
            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var id = request.RequestUri!.AbsolutePath.Split('/')[^1];
                    var response = new HttpResponseMessage(id == "odd" ? System.Net.HttpStatusCode.NotFound : System.Net.HttpStatusCode.OK)
                    {
                        RequestMessage = request,
                        Content = request.Method == HttpMethod.Head || id == "odd"
                            ? new ByteArrayContent([])
                            : new StringContent("""{"id":"1"}""", System.Text.Encoding.UTF8, "application/json"),
                    };
                    if (id == "all")
                    {
                        response.Headers.TryAddWithoutValidation("etag", "\"v1\"");
                        response.Headers.TryAddWithoutValidation("X-Rate-Limit", "5");
                        response.Headers.TryAddWithoutValidation("X-Expires-After", "2026-10-13T08:30:00.0000000+02:00");
                        response.Content.Headers.TryAddWithoutValidation("Last-Modified", "Tue, 13 Oct 2026 06:30:00 GMT");
                        response.Headers.TryAddWithoutValidation("X-Tags", "a, b");
                        response.Headers.TryAddWithoutValidation("X-Flag", "true");
                    }
                    else if (id == "odd")
                    {
                        response.Headers.TryAddWithoutValidation("X-Rate-Limit", "soon");
                        response.Headers.TryAddWithoutValidation("X-Expires-After", "later");
                        response.Headers.TryAddWithoutValidation("X-Flag", "maybe");
                        response.Headers.TryAddWithoutValidation("Message", "why");
                        response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "9000000000");
                    }

                    return Task.FromResult(response);
                }
            }
        }
        """";

    /// <summary>
    /// The EndpointPerOperation client reads them back: the result has a typed property per header the operation's
    /// responses declare (<c>ETagHeader</c>, <c>XRateLimitHeader</c>) - whatever the casing the server sent the name
    /// in, a content header (<c>Last-Modified</c>) included. A header that is absent, or is not what the specification
    /// says, is null: reading a header never throws.
    /// </summary>
    [Fact]
    public async Task OperationClient_Result_ReadsTheHeadersTheResponsesDeclare()
    {
        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, ResponseHeadersYaml);
        sources.Add(("Probe.cs", ResponseHeadersClientProbe));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var run = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("RunAsync")!;

        var lines = await (Task<List<string>>)run.Invoke(null, [])!;

        Assert.Equal(
            [
                "all: 200 ETag=\"v1\" X-Rate-Limit=5 X-Expires-After=2026-10-13T08:30:00.0000000+02:00 Last-Modified=2026-10-13T06:30:00.0000000+00:00 X-Tags=a|b X-Flag=True Message=(null) X-RateLimit-Remaining=(null)",
                "none: 200 ETag=(null) X-Rate-Limit=(null) X-Expires-After=(null) Last-Modified=(null) X-Tags=(null) X-Flag=(null) Message=(null) X-RateLimit-Remaining=(null)",
                "odd: 404 ETag=(null) X-Rate-Limit=(null) X-Expires-After=(null) Last-Modified=(null) X-Tags=(null) X-Flag=(null) Message=why X-RateLimit-Remaining=9000000000",
                "head: 200 ETag=\"v1\"",
            ],
            lines);

        // The interface a consumer mocks has them too; the body's own header is no property
        var contract = assembly.GetTypes().Single(t => t.Name == "IGetThingEndpointResult");
        Assert.NotNull(contract.GetProperty("ETagHeader"));
        Assert.Null(contract.GetProperty("ContentTypeHeader"));
    }

    /// <summary>
    /// Two path segments, so each has its own namespaces, and an operation whose only use of an enum is a header of a
    /// response without a body: the result's file finds the enum's type all the same, on the server and on the
    /// EndpointPerOperation client, a closed or an extensible enum.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnumHeaders_CompileWhereTheEnumIsUsedByAHeaderAlone(
        bool extensibleEnums)
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: ErrorBodies
              version: 1.0.0
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '200':
                      description: The things
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Thing'
              /status:
                head:
                  operationId: probeStatus
                  responses:
                    '204':
                      description: Up
                      headers:
                        X-Mode:
                          schema:
                            $ref: '#/components/schemas/Mode'
                        X-Modes:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Mode'
                        X-Priority:
                          schema:
                            $ref: '#/components/schemas/Priority'
            components:
              schemas:
                Thing:
                  type: object
                  required: [id]
                  properties:
                    id:
                      type: string
                Mode:
                  type: string
                  enum: [fast, slow-and-steady]
                Priority:
                  type: integer
                  enum: [1, 2, 3]
            """;
        var server = Generate(new ApiServerGenerator(), ".atc-rest-api-server", ServerMarker, includeMinimalApi: true, yaml);
        Assert.Contains(server, s => s.Source.Contains("Mode? xMode = null", StringComparison.Ordinal));
        CompilationVerificationHarness.EmitAndLoad(server, includeMinimalApi: true);

        var marker = $$"""
            {
              "generationMode": "EndpointPerOperation",
              "errorResponseFormat": "ProblemDetails",
              "extensibleEnums": {{(extensibleEnums ? "true" : "false")}}
            }
            """;
        var client = Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, yaml);
        Assert.Contains(client, s => s.Source.Contains("Mode? XModeHeader", StringComparison.Ordinal));
        CompilationVerificationHarness.EmitAndLoad(client);

        var typedMarker = $$"""
            {
              "generationMode": "TypedClient",
              "extensibleEnums": {{(extensibleEnums ? "true" : "false")}}
            }
            """;
        var typed = Generate(new ApiClientGenerator(), ".atc-rest-api-client", typedMarker, includeMinimalApi: false, yaml);
        Assert.Contains(typed, s => s.Source.Contains("class ProbeStatusResponseHeaders", StringComparison.Ordinal));
        CompilationVerificationHarness.EmitAndLoad(typed);
    }

    private const string TypedClientHeadersProbe = """"
        using ErrorBodies.Generated;
        using ErrorBodies.Generated.Models;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
            public static async Task<List<string>> RunAsync()
            {
                using var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") };
                var client = new ErrorBodiesClient(http);
                var lines = new List<string>();
        #if RESULT_STYLE
                var ok = await client.GetStatusAsync(new GetStatusParameters("ok"));
                lines.Add(Describe($"ok {(int)ok.StatusCode}", new GetStatusResponseHeaders(ok.Headers)));
                var down = await client.GetStatusAsync(new GetStatusParameters("down"));
                lines.Add(Describe($"down {(int)down.StatusCode}", new GetStatusResponseHeaders(down.Headers)));
        #else
                try
                {
                    await client.GetStatusAsync(new GetStatusParameters("down"));
                    lines.Add("down: no exception");
                }
                catch (ApiException exception)
                {
                    lines.Add(Describe($"down {(int)exception.StatusCode!}", new GetStatusResponseHeaders(exception.Headers)));
                }
        #endif
                return lines;
            }

            private static string Describe(string name, GetStatusResponseHeaders headers)
            {
                var window = headers.XWindowHeader is { } w ? $"{w.Start}/{Show(w.End)}/{Show(w.Mode.ToWireValue())}" : null;
                return $"{name}: X-Mode={Show(headers.XModeHeader.ToWireValue())} X-Priority={Show((int?)headers.XPriorityHeader)} X-Level={Show(headers.XLevelHeader.ToWireValue())} X-Window={Show(window)}";
            }

            private static string Show(object? value)
                => value is null ? "(null)" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var down = request.RequestUri!.AbsolutePath.Contains("/down/", StringComparison.Ordinal);
                    var response = new HttpResponseMessage(down ? System.Net.HttpStatusCode.ServiceUnavailable : System.Net.HttpStatusCode.OK)
                    {
                        RequestMessage = request,
                        Content = down
                            ? new ByteArrayContent([])
                            : new StringContent("""{"id":"1"}""", System.Text.Encoding.UTF8, "application/json"),
                    };
                    if (down)
                    {
                        response.Headers.TryAddWithoutValidation("x-mode", "fast");
                    }
                    else
                    {
                        response.Headers.TryAddWithoutValidation("X-Mode", "slow-and-steady");
                        response.Headers.TryAddWithoutValidation("X-Priority", "3");
                        response.Headers.TryAddWithoutValidation("X-Level", "low");
                        response.Headers.TryAddWithoutValidation("X-Window", "start,4,mode,fast");
                    }

                    return Task.FromResult(response);
                }
            }
        }
        """";

    /// <summary>
    /// The typed client has a <c>{Operation}ResponseHeaders</c> per operation whose responses declare headers: the
    /// properties of the EndpointPerOperation result, built from any headers - a Result-style response's, or the
    /// <c>ApiException</c> of the Throw style - whatever their casing.
    /// </summary>
    [Theory]
    [InlineData("Result")]
    [InlineData("Throw")]
    public async Task TypedClient_ResponseHeaders_ReadTheHeadersTheResponsesDeclare(
        string resultStyle)
    {
        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientTypedMarker(resultStyle), includeMinimalApi: false, ResponseHeadersYaml);
        var probe = resultStyle == "Result" ? "#define RESULT_STYLE\n" + TypedClientHeadersProbe : TypedClientHeadersProbe;
        sources.Add(("Probe.cs", probe));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var run = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("RunAsync")!;

        var lines = await (Task<List<string>>)run.Invoke(null, [])!;

        Assert.Equal(
            resultStyle == "Result"
                ? [
                    "ok 200: X-Mode=slow-and-steady X-Priority=3 X-Level=low X-Window=4/(null)/fast",
                    "down 503: X-Mode=fast X-Priority=(null) X-Level=(null) X-Window=(null)",
                ]
                : ["down 503: X-Mode=fast X-Priority=(null) X-Level=(null) X-Window=(null)"],
            lines);
    }

    private const string StatusHeadersClientProbe = """"
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated;
        using ErrorBodies.Generated.Client;
        using ErrorBodies.Generated.Endpoints.Interfaces;
        using ErrorBodies.Generated.Models;

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

                var lines = new List<string>();
                foreach (var scenario in new[] { "all", "unavailable", "odd", "none" })
                {
                    StubHandler.Scenario = scenario;
                    var result = await provider.GetRequiredService<IGetStatusEndpoint>().ExecuteAsync(new GetStatusParameters("1"));
                    var modes = result.XModesHeader is null ? null : string.Join("|", result.XModesHeader.Select(m => m.ToWireValue()));
                    var levels = result.XLevelsHeader is null ? null : string.Join("|", result.XLevelsHeader.Select(l => l.ToWireValue()));
                    lines.Add($"{scenario}: {(int)result.StatusCode} X-Mode={Show(result.XModeHeader.ToWireValue())} X-Modes={Show(modes)} X-Priority={Show((int?)result.XPriorityHeader)} X-Ratio={Show(result.XRatioHeader)} X-Level={Show(result.XLevelHeader.ToWireValue())} X-Levels={Show(levels)} X-Window={Show(DescribeWindow(result.XWindowHeader))} X-Window-Exploded={Show(DescribeWindow(result.XWindowExplodedHeader))}");
                }

                return lines;
            }

            private static string Show(object? value)
                => value is null ? "(null)" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;

            private static string? DescribeWindow(Window? window)
                => window is null ? null : $"{window.Start}/{Show(window.End)}/{Show(window.Mode.ToWireValue())}";

            // "all": every header as a server writes it. "unavailable": a 503 with its one header. "odd": an enum value
            // the specification does not list, and an integer enum value it does not list. "none": no header.
            private sealed class StubHandler : HttpMessageHandler
            {
                public static string Scenario { get; set; } = "none";

                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var response = new HttpResponseMessage(Scenario == "unavailable" ? System.Net.HttpStatusCode.ServiceUnavailable : System.Net.HttpStatusCode.OK)
                    {
                        RequestMessage = request,
                        Content = Scenario == "unavailable"
                            ? new ByteArrayContent([])
                            : new StringContent("""{"id":"1"}""", System.Text.Encoding.UTF8, "application/json"),
                    };
                    switch (Scenario)
                    {
                        case "all":
                            response.Headers.TryAddWithoutValidation("X-Mode", "slow-and-steady");
                            response.Headers.TryAddWithoutValidation("X-Modes", "fast, slow-and-steady");
                            response.Headers.TryAddWithoutValidation("X-Priority", "2");
                            response.Headers.TryAddWithoutValidation("X-Ratio", "0.5");
                            response.Headers.TryAddWithoutValidation("X-Level", "very-high");
                            response.Headers.TryAddWithoutValidation("X-Levels", "low, very-high");
                            response.Headers.TryAddWithoutValidation("X-Window", "start,1,end,5,mode,slow-and-steady");
                            response.Headers.TryAddWithoutValidation("X-Window-Exploded", "start=2, end=7");
                            break;
                        case "unavailable":
                            response.Headers.TryAddWithoutValidation("x-mode", "fast");
                            break;
                        case "odd":
                            response.Headers.TryAddWithoutValidation("X-Mode", "turbo");
                            response.Headers.TryAddWithoutValidation("X-Modes", "fast,warp");
                            response.Headers.TryAddWithoutValidation("X-Priority", "7");
                            response.Headers.TryAddWithoutValidation("X-Ratio", "half");
                            response.Headers.TryAddWithoutValidation("X-Level", "medium");
                            response.Headers.TryAddWithoutValidation("X-Window", "end,5");
                            response.Headers.TryAddWithoutValidation("X-Window-Exploded", "start=soon");
                            break;
                    }

                    return Task.FromResult(response);
                }
            }
        }
        """";

    /// <summary>
    /// An enum header is the generated enum on the client result, read from its wire value (<c>slow-and-steady</c>,
    /// not the member name), as an array of them is; an integer enum and a <c>float</c> are typed the same way, on a
    /// success and on an error response. A value the specification does not list is kept by an extensible enum (it is
    /// open, as in a body) and is null for the classic enum - reading a header never throws.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OperationClient_Result_ReadsEnumAndNumberHeadersAsTheirTypes(
        bool extensibleEnums)
    {
        var marker = $$"""
            {
              "generationMode": "EndpointPerOperation",
              "errorResponseFormat": "ProblemDetails",
              "extensibleEnums": {{(extensibleEnums ? "true" : "false")}}
            }
            """;
        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, ResponseHeadersYaml);
        sources.Add(("Probe.cs", StatusHeadersClientProbe));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var run = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("RunAsync")!;

        var lines = await (Task<List<string>>)run.Invoke(null, [])!;

        Assert.Equal(
            [
                "all: 200 X-Mode=slow-and-steady X-Modes=fast|slow-and-steady X-Priority=2 X-Ratio=0.5 X-Level=very-high X-Levels=low|very-high X-Window=1/5/slow-and-steady X-Window-Exploded=2/7/(null)",
                "unavailable: 503 X-Mode=fast X-Modes=(null) X-Priority=(null) X-Ratio=(null) X-Level=(null) X-Levels=(null) X-Window=(null) X-Window-Exploded=(null)",
                extensibleEnums
                    ? "odd: 200 X-Mode=turbo X-Modes=fast|warp X-Priority=(null) X-Ratio=(null) X-Level=medium X-Levels=(null) X-Window=(null) X-Window-Exploded=(null)"
                    : "odd: 200 X-Mode=(null) X-Modes=(null) X-Priority=(null) X-Ratio=(null) X-Level=(null) X-Levels=(null) X-Window=(null) X-Window-Exploded=(null)",
                "none: 200 X-Mode=(null) X-Modes=(null) X-Priority=(null) X-Ratio=(null) X-Level=(null) X-Levels=(null) X-Window=(null) X-Window-Exploded=(null)",
            ],
            lines);

        var contract = assembly.GetTypes().Single(t => t.Name == "IGetStatusEndpointResult");
        Assert.Equal("Mode", Nullable.GetUnderlyingType(contract.GetProperty("XModeHeader")!.PropertyType)!.Name);
        Assert.Equal(typeof(float?), contract.GetProperty("XRatioHeader")!.PropertyType);

        // An enum a header declares inline is generated for it: {Operation}{Header}Header
        Assert.Equal("GetStatusXLevelHeader", Nullable.GetUnderlyingType(contract.GetProperty("XLevelHeader")!.PropertyType)!.Name);
    }
}