namespace PetStoreFull.Api.Domain.ApiHandlers.Stores;

/// <summary>
/// Handler business logic for the DeleteOrder operation.
/// </summary>
public sealed class DeleteOrderHandler : IDeleteOrderHandler
{
    public Task<DeleteOrderResult> ExecuteAsync(
        DeleteOrderParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement deleteOrder logic
        throw new NotImplementedException("deleteOrder not implemented");
    }
}