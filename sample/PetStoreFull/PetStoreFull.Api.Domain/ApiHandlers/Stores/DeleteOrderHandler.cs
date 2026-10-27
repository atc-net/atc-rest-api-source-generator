namespace PetStoreFull.Api.Domain.ApiHandlers.Stores;

/// <summary>
/// Handler business logic for the DeleteOrder operation.
/// </summary>
public sealed class DeleteOrderHandler : IDeleteOrderHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public DeleteOrderHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<DeleteOrderResult> ExecuteAsync(
        DeleteOrderParameters parameters,
        CancellationToken cancellationToken = default)
        => Task.FromResult(repository.RemoveOrder(parameters.OrderId)
            ? DeleteOrderResult.Ok()
            : DeleteOrderResult.NotFound($"Order {parameters.OrderId} not found"));
}