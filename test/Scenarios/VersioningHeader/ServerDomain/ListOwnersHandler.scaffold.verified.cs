namespace VersioningHeader.ApiHandlers;

/// <summary>
/// Handler business logic for the ListOwners operation.
/// </summary>
public sealed class ListOwnersHandler : IListOwnersHandler
{
    /// <summary>
    /// Executes the ListOwners operation.
    /// </summary>
    public Task<ListOwnersResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listOwners logic
        throw new NotImplementedException("listOwners not implemented");
    }
}