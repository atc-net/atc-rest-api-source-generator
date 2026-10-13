namespace Showcase.BlazorApp.Services;

/// <summary>
/// Gateway service - Streams operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// Opens the price stream in one of the five formats. Each format is an operation with a result class of its
    /// own; all derive from <see cref="StreamingEndpointResponse{T}"/>, so the items are read the same way.
    /// </summary>
    public async Task<PriceStreamCall> OpenPriceStreamAsync(
        PriceStreamFormat format,
        int count,
        bool closed,
        CancellationToken cancellationToken = default)
    {
        switch (format)
        {
            case PriceStreamFormat.JsonLines:
            {
                var result = await listPriceLinesEndpoint
                    .ExecuteAsync(new ListPriceLinesParameters(count, closed), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new PriceStreamCall(result, result.XStreamFormatHeader, result.IsServiceUnavailable ? result.ServiceUnavailableContent : null, result.RetryAfterHeader);
            }

            case PriceStreamFormat.JsonArray:
            {
                var result = await listPriceElementsEndpoint
                    .ExecuteAsync(new ListPriceElementsParameters(count, closed), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new PriceStreamCall(result, result.XStreamFormatHeader, result.IsServiceUnavailable ? result.ServiceUnavailableContent : null, result.RetryAfterHeader);
            }

            case PriceStreamFormat.JsonSequence:
            {
                var result = await listPriceRecordsEndpoint
                    .ExecuteAsync(new ListPriceRecordsParameters(count, closed), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new PriceStreamCall(result, result.XStreamFormatHeader, result.IsServiceUnavailable ? result.ServiceUnavailableContent : null, result.RetryAfterHeader);
            }

            case PriceStreamFormat.MultipartMixed:
            {
                var result = await listPricePartsEndpoint
                    .ExecuteAsync(new ListPricePartsParameters(count, closed), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new PriceStreamCall(result, result.XStreamFormatHeader, result.IsServiceUnavailable ? result.ServiceUnavailableContent : null, result.RetryAfterHeader);
            }

            default:
            {
                var result = await listPriceEventsEndpoint
                    .ExecuteAsync(new ListPriceEventsParameters(count, closed), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return new PriceStreamCall(result, result.XStreamFormatHeader, result.IsServiceUnavailable ? result.ServiceUnavailableContent : null, result.RetryAfterHeader);
            }
        }
    }
}