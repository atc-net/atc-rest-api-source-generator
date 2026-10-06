namespace NexusSample.Api.Domain.ApiHandlers.EdgeProvider;

/// <summary>
/// Handler business logic for the ScanOpcUaConfigurationsByProviderDeviceId operation.
/// </summary>
public sealed class ScanOpcUaConfigurationsByProviderDeviceIdHandler : IScanOpcUaConfigurationsByProviderDeviceIdHandler
{
    private readonly DeviceInMemoryRepository repository;

    public ScanOpcUaConfigurationsByProviderDeviceIdHandler(
        DeviceInMemoryRepository repository)
        => this.repository = repository;

    public Task<ScanOpcUaConfigurationsByProviderDeviceIdResult> ExecuteAsync(
        ScanOpcUaConfigurationsByProviderDeviceIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!repository.Exists(parameters.ProviderDeviceId))
        {
            return Task.FromResult(ScanOpcUaConfigurationsByProviderDeviceIdResult.NotFound($"Device {parameters.ProviderDeviceId} not found"));
        }

        // Only an IoT Edge gateway hosts OPC UA configurations to scan
        if (!repository.IsEdgeGateway(parameters.ProviderDeviceId))
        {
            return Task.FromResult(ScanOpcUaConfigurationsByProviderDeviceIdResult.PreconditionFailed($"Device {parameters.ProviderDeviceId} is not an IoT Edge gateway"));
        }

        var scanKind = parameters.Request.ForceFullScan == true ? "full" : "incremental";

        return Task.FromResult(ScanOpcUaConfigurationsByProviderDeviceIdResult.Ok($"OPC UA {scanKind} scan started for device {parameters.ProviderDeviceId}"));
    }
}