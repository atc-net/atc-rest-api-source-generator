namespace Polymorphism.ApiHandlers;

/// <summary>
/// Handler business logic for the CreatePayment operation.
/// </summary>
public sealed class CreatePaymentHandler : ICreatePaymentHandler
{
    public Task<CreatePaymentResult> ExecuteAsync(
        CreatePaymentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createPayment logic
        throw new NotImplementedException("createPayment not implemented");
    }
}