namespace Retry.ApiHandlers;

/// <summary>
/// Handler business logic for the ListOrders operation.
/// </summary>
public sealed class ListOrdersHandler : IListOrdersHandler
{
    /// <summary>
    /// Executes the ListOrders operation.
    /// </summary>
    public Task<ListOrdersResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listOrders logic
        throw new NotImplementedException("listOrders not implemented");
    }
}