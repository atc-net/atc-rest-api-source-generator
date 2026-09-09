namespace Retry.ApiHandlers;

/// <summary>
/// Handler business logic for the ProcessPayment operation.
/// </summary>
public sealed class ProcessPaymentHandler : IProcessPaymentHandler
{
    public Task<ProcessPaymentResult> ExecuteAsync(
        ProcessPaymentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement processPayment logic
        throw new NotImplementedException("processPayment not implemented");
    }
}