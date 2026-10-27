namespace Showcase.Api.Domain.ApiHandlers.Streams;

/// <summary>
/// Handler business logic for the ListPriceEvents operation: live prices as Server-Sent Events.
/// The handler yields the events - each with the price's sequence number as its id - and the generated endpoint
/// writes them as text/event-stream: "id: {n}", "data: {json}" and a blank line. A client that sends the last id
/// it read as Last-Event-ID gets the stream from the price after it.
/// </summary>
public sealed class ListPriceEventsHandler : IListPriceEventsHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Api.Domain.ApiHandlers.Streams.ListPriceEvents");

    /// <summary>
    /// Executes the ListPriceEvents operation.
    /// </summary>
    public Task<ListPriceEventsResult> ExecuteAsync(
        ListPriceEventsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ListPriceEvents");

        // A closed exchange: 503 with a problem and the Retry-After header the response declares
        if (parameters.Closed.GetValueOrDefault())
        {
            return Task.FromResult(ListPriceEventsResult.ServiceUnavailable(PriceFeed.ClosedMessage, retryAfter: PriceFeed.RetryAfterSeconds));
        }

        var isResumed = long.TryParse(parameters.LastEventId, NumberStyles.None, CultureInfo.InvariantCulture, out var lastSequence);
        var events = EventsAsync(
            parameters.Count ?? PriceFeed.DefaultCount,
            firstSequence: isResumed ? lastSequence + 1 : 1,
            dropAfter: isResumed ? null : parameters.DropAfter,
            cancellationToken);

        return Task.FromResult(ListPriceEventsResult.Ok(events, xStreamFormat: "Server-Sent Events"));
    }

    private static async IAsyncEnumerable<SseItem<PriceTick>> EventsAsync(
        int count,
        long firstSequence,
        int? dropAfter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sent = 0;
        await foreach (var tick in PriceFeed.TicksAsync(count, cancellationToken, firstSequence))
        {
            if (sent == dropAfter)
            {
                // What a network does now and then: the connection ends in the middle of the stream
                throw new IOException($"The price stream was dropped after {sent} events (dropAfter).");
            }

            yield return new SseItem<PriceTick>(tick) { EventId = tick.Sequence.ToString(CultureInfo.InvariantCulture) };
            sent++;
        }
    }
}