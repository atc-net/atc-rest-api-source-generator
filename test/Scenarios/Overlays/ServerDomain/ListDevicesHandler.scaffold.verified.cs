namespace Overlays.ApiHandlers;

/// <summary>
/// Handler business logic for the ListDevices operation.
/// </summary>
public sealed class ListDevicesHandler : IListDevicesHandler
{
    /// <summary>
    /// Executes the ListDevices operation.
    /// </summary>
    public Task<ListDevicesResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listDevices logic
        throw new NotImplementedException("listDevices not implemented");
    }
}