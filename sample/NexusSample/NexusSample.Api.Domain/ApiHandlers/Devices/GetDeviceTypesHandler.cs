namespace NexusSample.Api.Domain.ApiHandlers.Devices;

/// <summary>
/// Handler business logic for the GetDeviceTypes operation.
/// </summary>
public sealed class GetDeviceTypesHandler : IGetDeviceTypesHandler
{
    public Task<GetDeviceTypesResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(GetDeviceTypesResult.Ok(DeviceInMemoryRepository.GetDeviceTypes()));
}