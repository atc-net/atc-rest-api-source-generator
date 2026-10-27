namespace PetStoreFull.Api.Domain.ApiHandlers.Stores;

/// <summary>
/// Handler business logic for the GetOrderById operation.
/// </summary>
public sealed class GetOrderByIdHandler : IGetOrderByIdHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public GetOrderByIdHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<GetOrderByIdResult> ExecuteAsync(
        GetOrderByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var order = repository.GetOrder(parameters.OrderId);

        return Task.FromResult(order is null
            ? GetOrderByIdResult.NotFound($"Order {parameters.OrderId} not found")
            : GetOrderByIdResult.Ok(order));
    }
}