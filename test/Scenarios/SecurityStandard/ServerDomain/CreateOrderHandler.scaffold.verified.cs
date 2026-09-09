namespace SecurityStandard.ApiHandlers;

/// <summary>
/// Handler business logic for the CreateOrder operation.
/// </summary>
public sealed class CreateOrderHandler : ICreateOrderHandler
{
    public Task<CreateOrderResult> ExecuteAsync(
        CreateOrderParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createOrder logic
        throw new NotImplementedException("createOrder not implemented");
    }
}