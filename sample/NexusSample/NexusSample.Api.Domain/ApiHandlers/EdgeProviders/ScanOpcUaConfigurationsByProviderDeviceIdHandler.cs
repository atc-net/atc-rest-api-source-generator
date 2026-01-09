namespace Contoso.IoT.Nexus.Api.Contracts.ApiHandlers.EdgeProviders;

/// <summary>
/// Handler business logic for the ScanOpcUaConfigurationsByProviderDeviceId operation.
/// </summary>
public sealed class ScanOpcUaConfigurationsByProviderDeviceIdHandler : IScanOpcUaConfigurationsByProviderDeviceIdHandler
{
    public Task<ScanOpcUaConfigurationsByProviderDeviceIdResult> ExecuteAsync(
        ScanOpcUaConfigurationsByProviderDeviceIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement scanOpcUaConfigurationsByProviderDeviceId logic
        throw new NotImplementedException("scanOpcUaConfigurationsByProviderDeviceId not implemented");
    }
}