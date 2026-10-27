namespace NexusSample.Api.Domain.ApiHandlers.Insights;

/// <summary>
/// Handler business logic for the GetInsightsDevices operation.
/// </summary>
public sealed class GetInsightsDevicesHandler : IGetInsightsDevicesHandler
{
    private readonly DeviceInMemoryRepository repository;

    public GetInsightsDevicesHandler(DeviceInMemoryRepository repository)
        => this.repository = repository;

    public Task<GetInsightsDevicesResult> ExecuteAsync(
        GetInsightsDevicesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // A continuation token from an earlier page wins over the page index
        var pageIndex = int.TryParse(parameters.ContinuationToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var skip)
            ? skip / parameters.PageSize
            : parameters.PageIndex ?? 0;

        return Task.FromResult(GetInsightsDevicesResult.Ok(
            repository.GetInsightPagesStreaming(parameters.PageSize, pageIndex, parameters.Filter, cancellationToken)));
    }
}