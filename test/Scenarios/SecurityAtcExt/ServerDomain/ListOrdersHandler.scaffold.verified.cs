namespace SecurityAtcExt.ApiHandlers;

/// <summary>
/// Handler business logic for the ListOrders operation.
/// </summary>
public sealed class ListOrdersHandler : IListOrdersHandler
{
    public Task<ListOrdersResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listOrders logic
        throw new NotImplementedException("listOrders not implemented");
    }
}