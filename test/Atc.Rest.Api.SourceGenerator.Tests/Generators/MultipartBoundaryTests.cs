namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A multipart/mixed stream whose <c>Content-Type</c> names no boundary cannot be split into its parts: both C#
/// clients say so with an <see cref="InvalidDataException"/>. They used to fall back to the generated server's own
/// boundary, so a foreign server's parts were never matched and the stream ended empty, silently.
/// </summary>
public sealed class MultipartBoundaryTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /feed:
            get:
              operationId: streamMixed
              x-return-async-enumerable: true
              responses:
                '200':
                  description: OK
                  content:
                    multipart/mixed:
                      schema:
                        $ref: '#/components/schemas/Thing'
        components:
          schemas:
            Thing:
              type: object
              properties:
                n: { type: integer }
        """;

    private const string ProbeSource = """"
        using System.Collections;
        using System.Net;
        using System.Reflection;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class BoundaryProbe
        {
            public static async Task<string> RunAsync(bool typedClient)
            {
                try
                {
                    return await ReadAsync(typedClient);
                }
                catch (Exception ex)
                {
                    return "throws:" + (ex is TargetInvocationException { InnerException: { } inner } ? inner : ex).GetType().Name;
                }
            }

            private static async Task<string> ReadAsync(bool typedClient)
            {
                var assembly = typeof(BoundaryProbe).Assembly;
                object stream;
                if (typedClient)
                {
                    var clientType = assembly.GetTypes().Single(t => t.IsClass && !t.IsAbstract && t.GetMethod("StreamMixedAsync") is not null);
                    var client = Activator.CreateInstance(clientType, new HttpClient(new StubHandler()) { BaseAddress = new Uri("http://stub/") })!;
                    stream = clientType.GetMethod("StreamMixedAsync")!.Invoke(client, [CancellationToken.None])!;
                }
                else
                {
                    var httpClientName = (string)assembly.GetTypes().Single(t => t.Name == "Constants").GetField("HttpClientName")!.GetValue(null)!;
                    var services = new ServiceCollection();
                    services
                        .AddHttpClient(httpClientName, c => c.BaseAddress = new Uri("http://stub/"))
                        .ConfigurePrimaryHttpMessageHandler(() => new StubHandler());
                    assembly.GetTypes().SelectMany(t => t.GetMethods()).Single(m => m.Name == "AddErrorBodiesEndpoints").Invoke(null, [services, null]);
                    var provider = services.BuildServiceProvider();
                    var endpointType = assembly.GetTypes().Single(t => t.Name == "IStreamMixedEndpoint");
                    var task = (Task)endpointType.GetMethod("ExecuteAsync")!.Invoke(provider.GetRequiredService(endpointType), [httpClientName, CancellationToken.None])!;
                    await task;
                    var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
                    stream = result.GetType().GetProperty("OkContent")!.GetValue(result)!;
                }

                {
                    var count = 0;
                    var enumerator = stream.GetType().GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)).GetMethod("GetAsyncEnumerator")!.Invoke(stream, [CancellationToken.None])!;
                    var moveNext = enumerator.GetType().GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerator<>)).GetMethod("MoveNextAsync")!;
                    while (await (ValueTask<bool>)moveNext.Invoke(enumerator, null)!)
                    {
                        count++;
                    }

                    return "items:" + count;
                }
            }

            private sealed class StubHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var content = new StringContent("--frame\r\nContent-Type: application/json\r\n\r\n{\"n\":1}\r\n--frame--\r\n");
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/mixed");
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
                }
            }
        }
        """";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AMultipartStreamWithoutABoundary_IsInvalidData(
        bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");
        var sources = ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false, Yaml);
        sources.Add(("BoundaryProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        var outcome = await (Task<string>)assembly.GetType("ErrorBodies.RuntimeTest.BoundaryProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [typedClient])!;

        Assert.Equal("throws:InvalidDataException", outcome);
    }
}