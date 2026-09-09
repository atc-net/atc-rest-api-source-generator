namespace SecurityHybrid.ApiHandlers;

/// <summary>
/// Handler business logic for the GetOrderTracking operation.
/// </summary>
public sealed class GetOrderTrackingHandler : IGetOrderTrackingHandler
{
    public Task<GetOrderTrackingResult> ExecuteAsync(
        GetOrderTrackingParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getOrderTracking logic
        throw new NotImplementedException("getOrderTracking not implemented");
    }
}