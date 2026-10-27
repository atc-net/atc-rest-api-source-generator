namespace NexusSample.Api.Domain.ApiHandlers.DeviceManagement;

/// <summary>
/// Handler business logic for the GetDeviceManagementDevices operation.
/// </summary>
public sealed class GetDeviceManagementDevicesHandler : IGetDeviceManagementDevicesHandler
{
    private readonly DeviceInMemoryRepository repository;

    public GetDeviceManagementDevicesHandler(
        DeviceInMemoryRepository repository)
        => this.repository = repository;

    public Task<GetDeviceManagementDevicesResult> ExecuteAsync(
        GetDeviceManagementDevicesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // A continuation token from an earlier page wins over the page index
        var pageIndex = int.TryParse(parameters.ContinuationToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var skip)
            ? skip / parameters.PageSize
            : parameters.PageIndex ?? 0;

        return Task.FromResult(GetDeviceManagementDevicesResult.Ok(
            repository.GetDeviceManagementPagesStreaming(parameters.PageSize, pageIndex, parameters.Filter, cancellationToken)));
    }
}