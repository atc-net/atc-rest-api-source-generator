namespace NexusSample.Api.Domain.ApiHandlers.Devices;

/// <summary>
/// Handler business logic for the GetDeviceDetails operation.
/// </summary>
public sealed class GetDeviceDetailsHandler : IGetDeviceDetailsHandler
{
    private readonly DeviceInMemoryRepository repository;

    public GetDeviceDetailsHandler(DeviceInMemoryRepository repository)
        => this.repository = repository;

    public Task<GetDeviceDetailsResult> ExecuteAsync(
        GetDeviceDetailsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var details = repository.GetDetails(parameters.DeviceId);

        return Task.FromResult(details is null
            ? GetDeviceDetailsResult.NotFound($"Device {parameters.DeviceId} not found")
            : GetDeviceDetailsResult.Ok(details));
    }
}