namespace VersioningUrlSegment.ApiHandlers;

/// <summary>
/// Handler business logic for the ListOwners operation.
/// </summary>
public sealed class ListOwnersHandler : IListOwnersHandler
{
    public Task<ListOwnersResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listOwners logic
        throw new NotImplementedException("listOwners not implemented");
    }
}