namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A Server-Sent Events operation of the EndpointPerOperation client: its result has the last event's
/// <c>EventName</c> and the server's <c>Retry</c> next to <c>LastEventId</c>; an operation that declares
/// <c>Last-Event-ID</c> gets <c>ReadResumingAsync</c>, which yields the items across reconnects - sending the last id
/// read, waiting the server's <c>retry:</c> after the stream ends and backing off while the server answers an error -
/// until the server answers 204 (or a 4xx), and says when it is connected and disconnected.
/// </summary>
public sealed class ServerSentEventResumeTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /events:
            get:
              operationId: listEvents
              parameters:
                - name: Last-Event-ID
                  in: header
                  required: false
                  schema:
                    type: string
              responses:
                '200':
                  description: The events
                  content:
                    text/event-stream:
                      schema:
                        $ref: '#/components/schemas/Tick'
                '503':
                  description: Not now
        components:
          schemas:
            Tick:
              type: object
              properties:
                seq:
                  type: integer
        """;

    private const string ProbeSource = """"
        using System.Net;
        using System.Text;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class ResumeProbe
        {
            private static readonly List<string> Calls = [];

            public static async Task<string[]> RunAsync()
            {
                Calls.Clear();
                var services = new ServiceCollection();
                services
                    .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                services.AddErrorBodiesEndpoints();
                using var provider = services.BuildServiceProvider();
                var endpoint = provider.GetRequiredService<IListEventsEndpoint>();

                var log = new List<string>();
                var policy = new ServerSentEventReconnectPolicy
                {
                    Delays = [TimeSpan.Zero],
                    OnConnected = () => log.Add("connected"),
                    OnDisconnected = (error, status) => log.Add($"disconnected {(status is null ? "-" : ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture))}"),
                };

                await foreach (var tick in endpoint.ReadResumingAsync(new ListEventsParameters(), policy))
                {
                    log.Add($"tick {tick?.Seq}");
                }

                // One result on its own: the last event's name and the server's retry
                var single = await endpoint.ExecuteAsync(new ListEventsParameters(LastEventId: "first"));
                await foreach (var _ in single.Content!)
                {
                }

                log.Add($"single {single.LastEventId}|{single.EventName ?? "-"}|{single.Retry?.TotalMilliseconds}");
                return [.. Calls, .. log];
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var lastEventId = request.Headers.TryGetValues("Last-Event-ID", out var values) ? string.Join(",", values) : "-";
                    Calls.Add($"call {lastEventId}");
                    return Task.FromResult((Calls.Count, lastEventId) switch
                    {
                        (1, "-") => Events("retry: 5\nid: 1\ndata: {\"seq\":1}\n\nid: 2\ndata: {\"seq\":2}\n\n"),
                        (2, "2") => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                        (3, "2") => Events("id: 3\ndata: {\"seq\":3}\n\n"),
                        (4, "3") => new HttpResponseMessage(HttpStatusCode.NoContent),
                        (_, "first") => Events("retry: 250\nevent: message\nid: 7\ndata: {\"seq\":7}\n\n"),
                        _ => new HttpResponseMessage(HttpStatusCode.BadRequest),
                    });
                }

                private static HttpResponseMessage Events(string text)
                    => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "text/event-stream") };
            }
        }
        """";

    [Fact]
    public async Task ReadResumingAsync_YieldsAcrossReconnects_SendingTheLastIdRead_UntilThe204()
    {
        // Arrange
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);
        sources.Add(("ResumeProbe.cs", WithUsings(sources, ProbeSource)));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        // Act
        var log = await (Task<string[]>)assembly.GetType("ErrorBodies.RuntimeTest.ResumeProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, null)!;

        // Assert
        Assert.Equal(
            [
                "call -",
                "call 2",
                "call 2",
                "call 3",
                "call first",
                "connected",
                "tick 1",
                "tick 2",
                "disconnected -",
                "disconnected 503",
                "connected",
                "tick 3",
                "disconnected -",
                "single 7|message|250",
            ],
            log);
    }

    [Fact]
    public async Task TheResume_KeepsTheGeneratedCodeTrimAndAotSafe()
    {
        // Arrange
        var client = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);

        // Act
        var problems = await AotAnalyzerHarness.AnalyzeAsync(client, includeMinimalApi: false);

        // Assert
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // The probe sees every generated namespace
    private static string WithUsings(
        List<(string HintName, string Source)> sources,
        string probe)
        => string.Join(
               Environment.NewLine,
               sources
                   .Select(s => Regex.Match(s.Source, @"^namespace (?<ns>[\w.]+);", RegexOptions.Multiline | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)).Groups["ns"].Value)
                   .Where(ns => ns.Length > 0)
                   .Distinct(StringComparer.Ordinal)
                   .Select(ns => $"global using {ns};")) +
           Environment.NewLine +
           probe;
}