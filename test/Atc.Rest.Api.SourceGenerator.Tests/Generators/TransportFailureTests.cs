namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// <c>"transportFailure": "result"</c> in an EndpointPerOperation client marker: a request that never got an answer - the
/// connection failed, it timed out without the caller cancelling it, a resilience pipeline rejected it - is answered
/// with a result (<c>IsTransportFailure</c>, a 503 whose problem has no text, for the caller to word in its own language)
/// instead of an exception; the caller's own
/// cancellation still throws. Without the key it throws, as before.
/// </summary>
public sealed class TransportFailureTests
{
    private const string Yaml = """
        openapi: 3.1.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /cameras:
            get:
              operationId: listCameras
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          type: string
          /events:
            get:
              operationId: listEvents
              responses:
                '200':
                  description: OK
                  content:
                    text/event-stream:
                      schema:
                        type: string
        """;

    private const string ProbeSource = """"
        using System.Net;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class TransportProbe
        {
            private static Exception toThrow = new HttpRequestException("No such host is known.");

            public static async Task<string[]> RunAsync()
            {
                var services = new ServiceCollection();
                services
                    .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new FailingHandler());
                services.AddErrorBodiesEndpoints();
                using var provider = services.BuildServiceProvider();
                var cameras = provider.GetRequiredService<IListCamerasEndpoint>();
                var events = provider.GetRequiredService<IListEventsEndpoint>();
                var log = new List<string>();

                toThrow = new HttpRequestException("No such host is known.");
                log.Add(Describe(await cameras.ExecuteAsync()));
                log.Add(Describe(await events.ExecuteAsync()));

                // A timeout the caller did not ask for
                toThrow = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
                log.Add(Describe(await cameras.ExecuteAsync()));

                // The caller's own cancellation is not a failure of the server
                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                try
                {
                    await cameras.ExecuteAsync(cancellationToken: cancelled.Token);
                    log.Add("no exception");
                }
                catch (OperationCanceledException)
                {
                    log.Add("cancelled");
                }

                return [.. log];
            }

            public static async Task<string> WithoutTheKeyAsync()
            {
                var services = new ServiceCollection();
                services
                    .AddHttpClient(Constants.HttpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => new FailingHandler());
                services.AddErrorBodiesEndpoints();
                using var provider = services.BuildServiceProvider();
                toThrow = new HttpRequestException("No such host is known.");
                try
                {
                    await provider.GetRequiredService<IListCamerasEndpoint>().ExecuteAsync();
                    return "no exception";
                }
                catch (HttpRequestException exception)
                {
                    return exception.Message;
                }
            }

            public static string Built()
                => Describe(ListCamerasEndpointResult.TransportFailure(new HttpRequestException("Connection refused")));

            private static string Describe(IEndpointResult result)
            {
                var status = result switch
                {
                    Atc.Rest.Client.IEndpointResponse response => (int)response.StatusCode,
                    Atc.Rest.Client.IStreamingEndpointResponse<string> streamed => (int)streamed.StatusCode,
                    _ => 0,
                };
                return $"{result.IsTransportFailure}|{status}|{result.Problem?.Detail ?? "-"}|{result.Problem?.ToDisplayText() ?? "-"}|{result.TransportException?.GetType().Name}";
            }

            private sealed class FailingHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromException<HttpResponseMessage>(toThrow);
                }
            }
        }
        """";

    [Fact]
    public async Task TheKey_AnswersAFailedRequestWithAResult_TheCallersCancellationStillThrows()
    {
        // Act
        var log = await (Task<string[]>)Probe("""{ "generationMode": "EndpointPerOperation", "transportFailure": "result" }""").GetMethod("RunAsync")!.Invoke(null, null)!;

        // Assert
        Assert.Equal(
            [
                "True|503|-|-|HttpRequestException",
                "True|503|-|-|HttpRequestException",
                "True|503|-|-|TaskCanceledException",
                "cancelled",
            ],
            log);
    }

    [Fact]
    public async Task WithoutTheKey_AFailedRequestThrows()
    {
        // Act
        var message = await (Task<string>)Probe("""{ "generationMode": "EndpointPerOperation" }""").GetMethod("WithoutTheKeyAsync")!.Invoke(null, null)!;

        // Assert
        Assert.Equal("No such host is known.", message);
    }

    [Fact]
    public void AResultOfATransportFailure_CanBeBuiltInATest()
    {
        // Act
        var built = (string)Probe("""{ "generationMode": "EndpointPerOperation" }""").GetMethod("Built")!.Invoke(null, null)!;

        // Assert
        Assert.Equal("True|503|-|-|HttpRequestException", built);
    }

    private static Type Probe(string marker)
    {
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("TransportProbe.cs", WithUsings(sources, ProbeSource)));
        return CompilationVerificationHarness.EmitAndLoad(sources).GetType("ErrorBodies.RuntimeTest.TransportProbe", throwOnError: true)!;
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