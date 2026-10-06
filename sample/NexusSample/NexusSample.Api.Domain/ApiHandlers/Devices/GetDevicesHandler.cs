namespace NexusSample.Api.Domain.ApiHandlers.Devices;

/// <summary>
/// Handler business logic for the GetDevices operation.
/// </summary>
public sealed class GetDevicesHandler : IGetDevicesHandler
{
    private readonly DeviceInMemoryRepository repository;

    public GetDevicesHandler(DeviceInMemoryRepository repository)
        => this.repository = repository;

    public Task<GetDevicesResult> ExecuteAsync(
        GetDevicesParameters parameters,
        CancellationToken cancellationToken = default)
        => Task.FromResult(GetDevicesResult.Ok(
            repository.GetIdentitiesStreaming(parameters.Filter, parameters.DeviceType, cancellationToken)));
}