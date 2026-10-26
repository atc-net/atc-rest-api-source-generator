namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A client that leaves while a stream's first item is awaited (<c>SequentialStreamWriter.StartAtFirstItem</c>): the
/// reading ends with the cancellation only - not a <c>NotSupportedException</c> from disposing the handler's iterator while
/// its first item is still pending, which the host logged as a 500 - within the first-item wait and after it, also
/// when the handler ignores the cancellation.
/// </summary>
public sealed class StreamFirstItemCancellationTests
{
    private const string ProbeSource = """
        using System.Runtime.CompilerServices;
        using Microsoft.AspNetCore.Http;

        namespace StreamingItemSchema.RuntimeTest;

        public static class FirstItemProbe
        {
            public static async Task<string> RunAsync(bool handlerHonoursCancellation, bool afterTheWait)
            {
                // After the wait: the headers have gone out without the first item, which is still awaited
                StreamingItemSchema.Generated.Streaming.SequentialStreamWriter.FirstItemWait = afterTheWait ? TimeSpan.FromMilliseconds(5) : TimeSpan.FromSeconds(1);
                using var requestAborted = new CancellationTokenSource();
                var context = new DefaultHttpContext();
                context.Response.Body = new MemoryStream();
                var items = handlerHonoursCancellation ? Honouring(default) : Ignoring();

                var reading = Task.Run(async () =>
                {
                    await foreach (var _ in StreamingItemSchema.Generated.Streaming.SequentialStreamWriter.StartAtFirstItem(items, context.Response, requestAborted.Token))
                    {
                    }
                });

                await Task.Delay(50);
                await requestAborted.CancelAsync();
                try
                {
                    await reading.WaitAsync(TimeSpan.FromSeconds(10));
                    return "ended";
                }
                catch (OperationCanceledException)
                {
                    return "cancelled";
                }
                catch (Exception exception)
                {
                    return exception.GetType().Name;
                }
            }

            // The first item never comes before the request is aborted
            private static async IAsyncEnumerable<int> Honouring([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                yield return 1;
            }

            private static async IAsyncEnumerable<int> Ignoring()
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                yield return 1;
            }
        }
        """;

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task AClientThatLeavesBeforeTheFirstItem_EndsTheReadingWithTheCancellation(
        bool handlerHonoursCancellation,
        bool afterTheWait)
    {
        // Arrange
        var sources = CompilationVerificationHarness.RunServer("StreamingItemSchema", "StreamingItemSchema.yaml");
        sources.Add(("FirstItemProbe.cs", ProbeSource));
        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);

        // Act
        var outcome = await (Task<string>)assembly.GetType("StreamingItemSchema.RuntimeTest.FirstItemProbe", throwOnError: true)!.GetMethod("RunAsync")!.Invoke(null, [handlerHonoursCancellation, afterTheWait])!;

        // Assert
        Assert.Equal("cancelled", outcome);
    }
}