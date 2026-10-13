namespace Showcase.Api.Domain.ApiHandlers.Streams;

/// <summary>
/// Handler business logic for the ListPriceElements operation: live prices as JSON array.
/// The handler only yields the prices; the generated endpoint writes them as
/// application/json: an array whose elements are written as the prices come.
/// </summary>
public sealed class ListPriceElementsHandler : IListPriceElementsHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Api.Domain.ApiHandlers.Streams.ListPriceElements");

    /// <summary>
    /// Executes the ListPriceElements operation.
    /// </summary>
    public Task<ListPriceElementsResult> ExecuteAsync(
        ListPriceElementsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ListPriceElements");

        // A closed exchange: 503 with a problem and the Retry-After header the response declares
        return Task.FromResult(parameters.Closed.GetValueOrDefault()
            ? ListPriceElementsResult.ServiceUnavailable(PriceFeed.ClosedMessage, retryAfter: PriceFeed.RetryAfterSeconds)
            : ListPriceElementsResult.Ok(PriceFeed.TicksAsync(parameters.Count ?? PriceFeed.DefaultCount, cancellationToken), xStreamFormat: "JSON array"));
    }
}