namespace SecurityHybrid.ApiHandlers;

/// <summary>
/// Handler business logic for the ListProducts operation.
/// </summary>
public sealed class ListProductsHandler : IListProductsHandler
{
    /// <summary>
    /// Executes the ListProducts operation.
    /// </summary>
    public Task<ListProductsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listProducts logic
        throw new NotImplementedException("listProducts not implemented");
    }
}