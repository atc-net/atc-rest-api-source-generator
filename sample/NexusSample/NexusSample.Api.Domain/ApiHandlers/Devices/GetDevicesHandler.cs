namespace Contoso.IoT.Nexus.Api.Contracts.ApiHandlers.Devices;

/// <summary>
/// Handler business logic for the GetDevices operation.
/// </summary>
public sealed class GetDevicesHandler : IGetDevicesHandler
{
    public Task<GetDevicesResult> ExecuteAsync(
        GetDevicesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getDevices logic
        throw new NotImplementedException("getDevices not implemented");
    }
}