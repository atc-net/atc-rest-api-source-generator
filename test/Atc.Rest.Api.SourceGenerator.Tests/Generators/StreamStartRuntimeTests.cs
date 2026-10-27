namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A streamed response answers with its status and headers at once, before its first item: a stream may be quiet for
/// minutes, and every generated client waits for the headers (<c>HttpCompletionOption.ResponseHeadersRead</c>), with its
/// timeouts counting until they come. Each of the five stream formats, on a live server whose handler yields nothing
/// for ten seconds.
/// </summary>
public class StreamStartRuntimeTests
{
    private const string Yaml = """
        openapi: 3.2.0
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /lines:
            get:
              operationId: quietLines
              responses:
                '200':
                  description: A stream
                  headers:
                    X-Stream-Id:
                      schema:
                        type: string
                  content:
                    application/jsonl:
                      itemSchema:
                        $ref: '#/components/schemas/Tick'
          /events:
            get:
              operationId: quietEvents
              responses:
                '200':
                  description: A stream
                  headers:
                    X-Stream-Id:
                      schema:
                        type: string
                  content:
                    text/event-stream:
                      itemSchema:
                        $ref: '#/components/schemas/Tick'
          /sequence:
            get:
              operationId: quietSequence
              responses:
                '200':
                  description: A stream
                  content:
                    application/json-seq:
                      itemSchema:
                        $ref: '#/components/schemas/Tick'
          /parts:
            get:
              operationId: quietParts
              responses:
                '200':
                  description: A stream
                  content:
                    multipart/mixed:
                      itemSchema:
                        $ref: '#/components/schemas/Tick'
          /array:
            get:
              operationId: quietArray
              x-return-async-enumerable: true
              responses:
                '200':
                  description: A stream
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Tick'
        components:
          schemas:
            Tick:
              type: object
              title: Tick
              required: [at]
              properties:
                at:
                  type: integer
        """;

    private const string Handlers = """
        using System.Runtime.CompilerServices;
        using Microsoft.Extensions.DependencyInjection;

        namespace ErrorBodies.RuntimeTest;

        public static class Quiet
        {
            // Nothing for ten seconds, then one item: what a live feed does between two events
            public static async IAsyncEnumerable<Tick> Ticks([EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default)
            {
                await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(10), cancellationToken);
                yield return new Tick(1);
            }
        }

        public sealed class QuietLinesHandler : IQuietLinesHandler
        {
            public System.Threading.Tasks.Task<QuietLinesResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(QuietLinesResult.Ok(Quiet.Ticks(cancellationToken), xStreamId: "lines-1"));
        }

        public sealed class QuietEventsHandler : IQuietEventsHandler
        {
            public System.Threading.Tasks.Task<QuietEventsResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(QuietEventsResult.Ok(Quiet.Ticks(cancellationToken), xStreamId: "events-1"));
        }

        public sealed class QuietSequenceHandler : IQuietSequenceHandler
        {
            public System.Threading.Tasks.Task<QuietSequenceResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(QuietSequenceResult.Ok(Quiet.Ticks(cancellationToken)));
        }

        public sealed class QuietPartsHandler : IQuietPartsHandler
        {
            public System.Threading.Tasks.Task<QuietPartsResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(QuietPartsResult.Ok(Quiet.Ticks(cancellationToken)));
        }

        public sealed class QuietArrayHandler : IQuietArrayHandler
        {
            public System.Threading.Tasks.Task<QuietArrayResult> ExecuteAsync(System.Threading.CancellationToken cancellationToken = default)
                => System.Threading.Tasks.Task.FromResult(QuietArrayResult.Ok(Quiet.Ticks(cancellationToken)));
        }

        public static class QuietRegistration
        {
            public static void Register(IServiceCollection services)
            {
                services.AddScoped<IQuietLinesHandler, QuietLinesHandler>();
                services.AddScoped<IQuietEventsHandler, QuietEventsHandler>();
                services.AddScoped<IQuietSequenceHandler, QuietSequenceHandler>();
                services.AddScoped<IQuietPartsHandler, QuietPartsHandler>();
                services.AddScoped<IQuietArrayHandler, QuietArrayHandler>();
            }
        }
        """;

    [Fact]
    public async Task EveryStreamFormat_SendsItsHeaders_BeforeItsFirstItem()
    {
        // Arrange
        await using var host = await XmlTestHost.StartAsync(Yaml, Handlers, "ErrorBodies.RuntimeTest.QuietRegistration");
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var late = new List<string>();

        // Act
        foreach (var (path, mediaType, streamId) in new (string, string, string?)[]
                 {
                     ("/lines", "application/jsonl", "lines-1"),
                     ("/events", "text/event-stream", "events-1"),
                     ("/sequence", "application/json-seq", null),
                     ("/parts", "multipart/mixed", null),
                     ("/array", "application/json", null),
                 })
        {
            // Headers within three seconds, where the first item takes ten
            using var headersTimeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            headersTimeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                using var response = await client.GetAsync(new Uri(path, UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, headersTimeout.Token);
                var contentType = response.Content.Headers.ContentType?.MediaType;
                var id = response.Headers.TryGetValues("X-Stream-Id", out var values) ? values.Single() : null;
                if (response.StatusCode != HttpStatusCode.OK || contentType != mediaType || id != streamId)
                {
                    late.Add($"{path}: {(int)response.StatusCode} {contentType} X-Stream-Id={id ?? "(none)"}");
                }
            }
            catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
            {
                late.Add($"{path}: no headers within three seconds");
            }
        }

        // Assert
        Assert.True(late.Count == 0, string.Join(Environment.NewLine, late));
    }
}