namespace Showcase.Api.Domain.ApiHandlers.Streams;

/// <summary>
/// Handler business logic for the ListPriceParts operation: live prices as multipart/mixed.
/// The handler only yields the prices; the generated endpoint writes them as
/// multipart/mixed: a part per price.
/// </summary>
public sealed class ListPricePartsHandler : IListPricePartsHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Api.Domain.ApiHandlers.Streams.ListPriceParts");

    /// <summary>
    /// Executes the ListPriceParts operation.
    /// </summary>
    public Task<ListPricePartsResult> ExecuteAsync(
        ListPricePartsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ListPriceParts");

        // A closed exchange: 503 with a problem and the Retry-After header the response declares
        return Task.FromResult(parameters.Closed.GetValueOrDefault()
            ? ListPricePartsResult.ServiceUnavailable(PriceFeed.ClosedMessage, retryAfter: PriceFeed.RetryAfterSeconds)
            : ListPricePartsResult.Ok(PriceFeed.TicksAsync(parameters.Count ?? PriceFeed.DefaultCount, cancellationToken), xStreamFormat: "multipart/mixed"));
    }
}