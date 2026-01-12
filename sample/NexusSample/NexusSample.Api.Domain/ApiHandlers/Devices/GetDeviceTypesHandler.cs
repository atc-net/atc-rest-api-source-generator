namespace NexusSample.Api.Domain.ApiHandlers.Devices;

/// <summary>
/// Handler business logic for the GetDeviceTypes operation.
/// </summary>
public sealed class GetDeviceTypesHandler : IGetDeviceTypesHandler
{
    public Task<GetDeviceTypesResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getDeviceTypes logic
        throw new NotImplementedException("getDeviceTypes not implemented");
    }
}