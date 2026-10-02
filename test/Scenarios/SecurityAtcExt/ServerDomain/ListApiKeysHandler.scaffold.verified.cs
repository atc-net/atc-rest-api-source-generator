namespace SecurityAtcExt.ApiHandlers;

/// <summary>
/// Handler business logic for the ListApiKeys operation.
/// </summary>
public sealed class ListApiKeysHandler : IListApiKeysHandler
{
    /// <summary>
    /// Executes the ListApiKeys operation.
    /// </summary>
    public Task<ListApiKeysResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listApiKeys logic
        throw new NotImplementedException("listApiKeys not implemented");
    }
}