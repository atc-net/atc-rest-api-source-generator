namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A Server-Sent Events operation of the EndpointPerOperation client: its result has the last event's
/// <c>EventName</c> and the server's <c>Retry</c> next to <c>LastEventId</c>; an operation that declares
/// <c>Last-Event-ID</c> gets <c>ReadResumingAsync</c>, which yields the items across reconnects - sending the last id
/// read, waiting the server's <c>retry:</c> after the stream ends and backing off while the server answers an error -
/// until the server answers 204 (or a 4xx), and says when it is connected (with the result, for its headers) and
/// disconnected - also when the caller stops reading. An event that is not JSON ends its connection, not the reading;
/// the waits run on the policy's <c>TimeProvider</c>.
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
                var policy = Policy(log);

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

            // An event whose data is not JSON: its connection ends, and the next one continues after it
            public static async Task<string[]> NotJsonAsync()
            {
                Calls.Clear();
                using var provider = Provider();
                var log = new List<string>();
                await foreach (var tick in provider.GetRequiredService<IListEventsEndpoint>().ReadResumingAsync(new ListEventsParameters(LastEventId: "bad"), Policy(log)))
                {
                    log.Add($"tick {tick?.Seq}");
                }

                return [.. Calls, .. log];
            }

            // The caller stops reading - by leaving the loop, or by cancelling it
            public static async Task<string[]> StopAsync()
            {
                Calls.Clear();
                using var provider = Provider();
                var endpoint = provider.GetRequiredService<IListEventsEndpoint>();
                var log = new List<string>();
                await foreach (var tick in endpoint.ReadResumingAsync(new ListEventsParameters(), Policy(log)))
                {
                    log.Add($"tick {tick?.Seq}");
                    break;
                }

                Calls.Clear();
                using var cancellation = new CancellationTokenSource();
                try
                {
                    await foreach (var tick in endpoint.ReadResumingAsync(new ListEventsParameters(), Policy(log), cancellationToken: cancellation.Token))
                    {
                        log.Add($"tick {tick?.Seq}");
                        await cancellation.CancelAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    log.Add("cancelled");
                }

                return [.. log];
            }

            private static ServiceProvider Provider()
            {
                var services = new ServiceCollection();
                services
                    .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                services.AddErrorBodiesEndpoints();
                return services.BuildServiceProvider();
            }

            // The default waits, on a clock that only notes them
            private static ServerSentEventReconnectPolicy Policy(List<string> log)
                => new()
                {
                    TimeProvider = new NotingTimeProvider(log),
                    OnConnected = result => log.Add($"connected {(result.Headers.TryGetValue("X-Contoso-Reset", out var reset) ? string.Join(",", reset) : "-")}"),
                    OnDisconnected = (error, status) => log.Add($"disconnected {(status is null ? "-" : ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture))} {error?.GetType().Name ?? "-"}"),
                };

            private sealed class NotingTimeProvider(List<string> log) : TimeProvider
            {
                public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
                {
                    log.Add($"wait {dueTime.TotalMilliseconds.ToString(global::System.Globalization.CultureInfo.InvariantCulture)}");
                    ThreadPool.QueueUserWorkItem(_ => callback(state));
                    return new DoneTimer();
                }

                private sealed class DoneTimer : ITimer
                {
                    public bool Change(TimeSpan dueTime, TimeSpan period) => true;

                    public void Dispose()
                    {
                    }

                    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
                }
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var lastEventId = request.Headers.TryGetValues("Last-Event-ID", out var values) ? string.Join(",", values) : "-";
                    Calls.Add($"call {lastEventId}");
                    return Task.FromResult((Calls.Count, lastEventId) switch
                    {
                        (1, "-") => Events("retry: 5\nid: 1\ndata: {\"seq\":1}\n\nid: 2\ndata: {\"seq\":2}\n\n", reset: true),
                        (1, "bad") => Events("id: 4\ndata: {\"seq\":4}\n\nid: 5\ndata: not json\n\nid: 6\ndata: {\"seq\":6}\n\n"),
                        (2, "5") => Events("id: 6\ndata: {\"seq\":6}\n\n"),
                        (3, "6") => new HttpResponseMessage(HttpStatusCode.NoContent),
                        (2, "2") => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                        (3, "2") => Events("id: 3\ndata: {\"seq\":3}\n\n"),
                        (4, "3") => new HttpResponseMessage(HttpStatusCode.NoContent),
                        (_, "first") => Events("retry: 250\nevent: message\nid: 7\ndata: {\"seq\":7}\n\n"),
                        _ => new HttpResponseMessage(HttpStatusCode.BadRequest),
                    });
                }

                private static HttpResponseMessage Events(string text, bool reset = false)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "text/event-stream") };
                    if (reset)
                    {
                        response.Headers.Add("X-Contoso-Reset", "true");
                    }

                    return response;
                }
            }
        }
        """";

    [Fact]
    public async Task ReadResumingAsync_YieldsAcrossReconnects_SendingTheLastIdRead_UntilThe204()
    {
        // Act
        var log = await RunAsync("RunAsync");

        // Assert - the server's retry after a stream that ended, the policy's first wait (never shorter) after the 503
        Assert.Equal(
            [
                "call -",
                "call 2",
                "call 2",
                "call 3",
                "call first",
                "connected true",
                "tick 1",
                "tick 2",
                "disconnected - -",
                "wait 5",
                "disconnected 503 -",
                "wait 1000",
                "connected -",
                "tick 3",
                "disconnected - -",
                "wait 5",
                "disconnected 204 -",
                "single 7|message|250",
            ],
            log);
    }

    [Fact]
    public async Task ReadResumingAsync_AnEventThatIsNotJson_EndsItsConnection_TheNextContinuesAfterIt()
    {
        // Act
        var log = await RunAsync("NotJsonAsync");

        // Assert
        Assert.Equal(
            [
                "call bad",
                "call 5",
                "call 6",
                "connected -",
                "tick 4",
                "disconnected - JsonException",
                "wait 1000",
                "connected -",
                "tick 6",
                "disconnected - -",
                "wait 1000",
                "disconnected 204 -",
            ],
            log);
    }

    [Fact]
    public async Task ReadResumingAsync_TheCallerStopping_IsADisconnect()
    {
        // Act
        var log = await RunAsync("StopAsync");

        // Assert
        Assert.Equal(
            [
                "connected true",
                "tick 1",
                "disconnected - -",
                "connected true",
                "tick 1",
                "disconnected - -",
                "cancelled",
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

    private static Task<string[]> RunAsync(string method)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml);
        sources.Add(("ResumeProbe.cs", WithUsings(sources, ProbeSource)));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        return (Task<string[]>)assembly.GetType("ErrorBodies.RuntimeTest.ResumeProbe", throwOnError: true)!.GetMethod(method)!.Invoke(null, null)!;
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