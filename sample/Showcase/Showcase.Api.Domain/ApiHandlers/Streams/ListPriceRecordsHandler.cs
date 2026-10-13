namespace Showcase.Api.Domain.ApiHandlers.Streams;

/// <summary>
/// Handler business logic for the ListPriceRecords operation: live prices as JSON Text Sequence.
/// The handler only yields the prices; the generated endpoint writes them as
/// application/json-seq (RFC 7464): a record per price - the record separator, the JSON, a line feed.
/// </summary>
public sealed class ListPriceRecordsHandler : IListPriceRecordsHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Api.Domain.ApiHandlers.Streams.ListPriceRecords");

    /// <summary>
    /// Executes the ListPriceRecords operation.
    /// </summary>
    public Task<ListPriceRecordsResult> ExecuteAsync(
        ListPriceRecordsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ListPriceRecords");

        // A closed exchange: 503 with a problem and the Retry-After header the response declares
        return Task.FromResult(parameters.Closed.GetValueOrDefault()
            ? ListPriceRecordsResult.ServiceUnavailable(PriceFeed.ClosedMessage, retryAfter: PriceFeed.RetryAfterSeconds)
            : ListPriceRecordsResult.Ok(PriceFeed.TicksAsync(parameters.Count ?? PriceFeed.DefaultCount, cancellationToken), xStreamFormat: "JSON Text Sequence"));
    }
}