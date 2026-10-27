namespace PetStoreFull.Api.Domain.ApiHandlers.Stores;

/// <summary>
/// Handler business logic for the PlaceOrder operation.
/// </summary>
public sealed class PlaceOrderHandler : IPlaceOrderHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public PlaceOrderHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<PlaceOrderResult> ExecuteAsync(
        PlaceOrderParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (parameters.Request.PetId is not { } petId || repository.GetPet(petId) is null)
        {
            return Task.FromResult(PlaceOrderResult.UnprocessableEntity("The order must name an existing pet"));
        }

        var order = repository.AddOrder(parameters.Request with
        {
            Status = parameters.Request.Status ?? OrderStatus.Placed,
            Complete = parameters.Request.Complete ?? false,
        });

        return Task.FromResult(PlaceOrderResult.Ok(order));
    }
}