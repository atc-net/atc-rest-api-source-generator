namespace PetStoreFull.Api.Domain.ApiHandlers.Stores;

/// <summary>
/// Handler business logic for the GetOrderById operation.
/// </summary>
public sealed class GetOrderByIdHandler : IGetOrderByIdHandler
{
    public Task<GetOrderByIdResult> ExecuteAsync(
        GetOrderByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getOrderById logic
        throw new NotImplementedException("getOrderById not implemented");
    }
}