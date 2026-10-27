namespace Showcase.Api.Domain.ApiHandlers.Streams;

/// <summary>
/// Handler business logic for the ListPriceLines operation: live prices as JSON Lines.
/// The handler only yields the prices; the generated endpoint writes them as
/// application/jsonl: a line per price.
/// </summary>
public sealed class ListPriceLinesHandler : IListPriceLinesHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Api.Domain.ApiHandlers.Streams.ListPriceLines");

    /// <summary>
    /// Executes the ListPriceLines operation.
    /// </summary>
    public Task<ListPriceLinesResult> ExecuteAsync(
        ListPriceLinesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ListPriceLines");

        // A closed exchange: 503 with a problem and the Retry-After header the response declares
        return Task.FromResult(parameters.Closed.GetValueOrDefault()
            ? ListPriceLinesResult.ServiceUnavailable(PriceFeed.ClosedMessage, retryAfter: PriceFeed.RetryAfterSeconds)
            : ListPriceLinesResult.Ok(PriceFeed.TicksAsync(parameters.Count ?? PriceFeed.DefaultCount, cancellationToken), xStreamFormat: "JSON Lines"));
    }
}