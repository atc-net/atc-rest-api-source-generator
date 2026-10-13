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
        """;

    private const string ResponseHeadersProbe = """"
        using ErrorBodies.Generated.Models;
        using ErrorBodies.Generated.Results;
        using Microsoft.AspNetCore.Http;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
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
}