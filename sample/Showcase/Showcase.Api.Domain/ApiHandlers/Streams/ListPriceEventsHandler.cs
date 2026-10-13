namespace Showcase.Api.Domain.ApiHandlers.Streams;

/// <summary>
/// Handler business logic for the ListPriceEvents operation: live prices as Server-Sent Events.
/// The handler only yields the prices; the generated endpoint writes them as
/// text/event-stream: an event per price - "data: {json}" and a blank line.
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
        return Task.FromResult(parameters.Closed.GetValueOrDefault()
            ? ListPriceEventsResult.ServiceUnavailable(PriceFeed.ClosedMessage, retryAfter: PriceFeed.RetryAfterSeconds)
            : ListPriceEventsResult.Ok(PriceFeed.TicksAsync(parameters.Count ?? PriceFeed.DefaultCount, cancellationToken), xStreamFormat: "Server-Sent Events"));
    }
}