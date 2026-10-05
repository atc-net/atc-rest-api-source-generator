namespace Overlays.ApiHandlers;

/// <summary>
/// Handler business logic for the GetDevice operation.
/// </summary>
public sealed class GetDeviceHandler : IGetDeviceHandler
{
    /// <summary>
    /// Executes the GetDevice operation.
    /// </summary>
    public Task<GetDeviceResult> ExecuteAsync(
        GetDeviceParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getDevice logic
        throw new NotImplementedException("getDevice not implemented");
    }
}