namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The generated C# clients dispose every response they do not hand to the caller: a buffered answer once it is read,
/// a failed one once its error is thrown, and a stream response whose body cannot be opened. Before, only the streaming
/// Throw-style methods disposed theirs. The specification's <c>x-return-stream</c> download pins that a TypedClient hands
/// it over as a <c>StreamedFile</c> whose disposal (as the probe's caller does) disposes the response, and that a failed
/// download is disposed by the client.
/// </summary>
public sealed class ClientResponseDisposalTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /thing:
            get:
              operationId: getThing
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
          /feed:
            get:
              operationId: streamFeed
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    text/event-stream:
                      schema:
                        $ref: '#/components/schemas/Thing'
          /file:
            get:
              operationId: downloadFile
              x-return-stream: true
              responses:
                '200':
                  description: OK
                  content:
                    application/octet-stream:
                      schema:
                        type: string
                        format: binary
        components:
          schemas:
            Thing:
              type: object
              properties:
                n: { type: integer }
        """;

    private const string ProbeSource = """"
        using System.Net;
        using System.Reflection;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class DisposalProbe
        {
            public static async Task<bool> RunAsync(string method, bool operationClient, HttpStatusCode status, bool failRead)
            {
                var handler = new StubHandler(status, failRead);
                try
                {
                    await CallAsync(method, operationClient, handler);
                }
                catch (Exception)
                {
                    // The outcome under test is the disposal, whatever the call threw
                }

                return handler.Content!.Disposed;
            }

            private static async Task CallAsync(string method, bool operationClient, StubHandler handler)
            {
                var assembly = typeof(DisposalProbe).Assembly;
                if (!operationClient)
                {
                    var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod(method + "Async") is not null);
                    var client = Activator.CreateInstance(clientType, new HttpClient(handler) { BaseAddress = new Uri("http://stub/") })!;
                    var result = clientType.GetMethod(method + "Async")!.Invoke(client, [CancellationToken.None])!;
                    if (result is Task task)
                    {
                        await task;

                        // A caller disposes what it owns: a StreamedFile, or the one in an EndpointResponse
                        var value = task.GetType().GetProperty("Result")?.GetValue(task);
                        var owned = value as IDisposable ?? value?.GetType().GetProperty("SuccessContent")?.GetValue(value);
                        (owned as IDisposable)?.Dispose();
                    }

                    return;
                }

                var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                var services = new ServiceCollection();
                services
                    .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                    .ConfigurePrimaryHttpMessageHandler(() => handler);
                assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, null]);
                var provider = services.BuildServiceProvider();
                var endpointType = assembly.GetTypes().Single(t => t.Name == "I" + method + "Endpoint");
                await (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(provider.GetRequiredService(endpointType), [httpClientName, CancellationToken.None, .. Enumerable.Repeat(Type.Missing, endpointType.GetMethod("ExecuteAsync")!.GetParameters().Length - 2)])!;
            }

            private sealed class StubHandler(HttpStatusCode status, bool failRead) : HttpMessageHandler
            {
                public TrackedContent? Content { get; private set; }

                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    Content = new TrackedContent(failRead);
                    return Task.FromResult(new HttpResponseMessage(status) { Content = Content });
                }
            }

            private sealed class TrackedContent : HttpContent
            {
                private readonly bool failRead;

                public TrackedContent(bool failRead)
                {
                    this.failRead = failRead;
                    Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(failRead ? "text/event-stream" : "application/json");
                }

                public bool Disposed { get; private set; }

                protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
                    => failRead
                        ? throw new IOException("The connection broke.")
                        : stream.WriteAsync("{\"n\":1}"u8.ToArray()).AsTask();

                protected override bool TryComputeLength(out long length)
                {
                    length = 0;
                    return false;
                }

                protected override void Dispose(bool disposing)
                {
                    Disposed = true;
                    base.Dispose(disposing);
                }
            }
        }
        """";

    public static TheoryData<string, string, HttpStatusCode, bool> Cases
        => new()
        {
            { "TypedClient Throw", "GetThing", HttpStatusCode.OK, false },
            { "TypedClient Throw", "GetThing", HttpStatusCode.InternalServerError, false },
            { "TypedClient Result", "GetThing", HttpStatusCode.OK, false },
            { "TypedClient Result", "GetThing", HttpStatusCode.InternalServerError, false },
            { "TypedClient Result", "StreamFeed", HttpStatusCode.OK, true },
            { "TypedClient Throw", "DownloadFile", HttpStatusCode.OK, false },
            { "TypedClient Throw", "DownloadFile", HttpStatusCode.InternalServerError, false },
            { "TypedClient Result", "DownloadFile", HttpStatusCode.OK, false },
            { "TypedClient Result", "DownloadFile", HttpStatusCode.InternalServerError, false },
            { "EndpointPerOperation", "StreamFeed", HttpStatusCode.OK, true },
            { "EndpointPerOperation", "DownloadFile", HttpStatusCode.OK, true },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task TheResponseIsDisposed_WhenTheCallerDoesNotOwnIt(
        string client,
        string method,
        HttpStatusCode status,
        bool failRead)
    {
        var operationClient = client == "EndpointPerOperation";
        var marker = operationClient
            ? ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails")
            : $$"""{ "generationMode": "TypedClient", "typedClientResultStyle": "{{client.Split(' ')[1]}}" }""";
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("DisposalProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var disposed = await (Task<bool>)assembly.GetType("ErrorBodies.RuntimeTest.DisposalProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [method, operationClient, status, failRead])!;

        Assert.True(disposed);
    }
}