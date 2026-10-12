namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

public sealed partial class ErrorResponseBodyRuntimeTests
{
    private const string HeadYaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /things/{id}:
            head:
              operationId: headThing
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: The thing exists
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
                '404':
                  description: Not found
        components:
          schemas:
            Thing:
              type: object
              required: [id]
              properties:
                id:
                  type: string
        """;

    private const string HeadProbe = """"
        using ErrorBodies.Generated;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
            public static async Task<string> RunAsync(bool result)
            {
                var lines = new List<string>();
                foreach (var id in new[] { "there", "missing" })
                {
                    using var http = new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") };
                    try
                    {
                        var call = new ErrorBodiesClient(http).HeadThingAsync(new HeadThingParameters(id));
                        await call;
                        var answer = call.GetType().GetProperty("Result")!.GetValue(call);
                        lines.Add(result
                            ? $"{id}: {(int)(System.Net.HttpStatusCode)answer!.GetType().GetProperty("StatusCode")!.GetValue(answer)!} IsSuccess={answer.GetType().GetProperty("IsSuccess")!.GetValue(answer)}"
                            : $"{id}: returned {(answer is null || answer.GetType().Name == "VoidTaskResult" ? "nothing" : answer.GetType().Name)}");
                    }
                    catch (Exception ex)
                    {
                        lines.Add($"{id}: {ex.GetType().Name} {(ex as HttpRequestException)?.StatusCode}");
                    }
                }

                return string.Join(" | ", lines);
            }

            // What a server answers a HEAD with: the headers of the GET's answer - a Content-Type, the Content-Length the
            // body would have had - and no body
            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var found = request.RequestUri!.AbsolutePath.EndsWith("/there", StringComparison.Ordinal);
                    var response = new HttpResponseMessage(found ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound)
                    {
                        RequestMessage = request,
                        Content = new ByteArrayContent([]),
                    };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(found ? "application/json" : "application/problem+json");
                    response.Content.Headers.ContentLength = 14;
                    return Task.FromResult(response);
                }
            }
        }
        """";

    /// <summary>
    /// A HEAD answer has no body, whatever its operation declares (a specification often gives its HEAD the content of
    /// its GET) and whatever Content-Type and Content-Length it carries: the typed client reads none - a 200 is a
    /// success without a value, a 404 is the error it is, and neither is a JSON error over the empty body.
    /// </summary>
    [Theory]
    [InlineData("Result", "there: 200 IsSuccess=True | missing: 404 IsSuccess=False")]
    [InlineData("Throw", "there: returned nothing | missing: ApiException NotFound")]
    public async Task TypedClient_Head_IsASuccessOrAnErrorWithoutABody(
        string resultStyle,
        string expected)
    {
        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientTypedMarker(resultStyle), includeMinimalApi: false, HeadYaml);
        sources.Add(("Probe.cs", HeadProbe));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var run = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("RunAsync")!;

        var outcome = await (Task<string>)run.Invoke(null, [string.Equals(resultStyle, "Result", StringComparison.Ordinal)])!;

        Assert.Equal(expected, outcome);
    }

    private const string HeadOperationProbe = """"
        using Atc.Rest.Client;
        using Microsoft.Extensions.DependencyInjection;
        using ErrorBodies.Generated;
        using ErrorBodies.Generated.Client;
        using ErrorBodies.Generated.Endpoints.Interfaces;

        namespace ErrorBodies.RuntimeTest;

        public static class Probe
        {
            public static async Task<string> RunAsync()
            {
                var services = new ServiceCollection();
                services
                    .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                services.AddErrorBodiesEndpoints();
                using var provider = services.BuildServiceProvider();

                var lines = new List<string>();
                foreach (var id in new[] { "there", "missing" })
                {
                    try
                    {
                        var result = await provider.GetRequiredService<IHeadThingEndpoint>().ExecuteAsync(new HeadThingParameters(id));
                        lines.Add($"{id}: {(int)result.StatusCode} IsOk={result.IsOk} IsNotFound={result.IsNotFound}");
                    }
                    catch (Exception ex)
                    {
                        lines.Add($"{id}: {ex.GetType().Name} {ex.Message}");
                    }
                }

                return string.Join(" | ", lines);
            }

            // What a server answers a HEAD with: the headers of the GET's answer - a Content-Type, the Content-Length the
            // body would have had - and no body
            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var found = request.RequestUri!.AbsolutePath.EndsWith("/there", StringComparison.Ordinal);
                    var response = new HttpResponseMessage(found ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound)
                    {
                        RequestMessage = request,
                        Content = new ByteArrayContent([]),
                    };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(found ? "application/json" : "application/problem+json");
                    response.Content.Headers.ContentLength = 14;
                    return Task.FromResult(response);
                }
            }
        }
        """";

    /// <summary>
    /// The same for the EndpointPerOperation client: a HEAD answer is its status - <c>IsOk</c>, <c>IsNotFound</c> - and
    /// building the result reads no body.
    /// </summary>
    [Fact]
    public async Task OperationClient_Head_IsItsStatusWithoutABody()
    {
        var sources = Generate(new ApiClientGenerator(), ".atc-rest-api-client", ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, HeadYaml);
        sources.Add(("Probe.cs", HeadOperationProbe));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var run = assembly.GetType("ErrorBodies.RuntimeTest.Probe", throwOnError: true)!.GetMethod("RunAsync")!;

        var outcome = await (Task<string>)run.Invoke(null, [])!;

        Assert.Equal("there: 200 IsOk=True IsNotFound=False | missing: 404 IsOk=False IsNotFound=True", outcome);
    }
}