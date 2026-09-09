namespace Polymorphism.ApiHandlers;

/// <summary>
/// Handler business logic for the ListPayments operation.
/// </summary>
public sealed class ListPaymentsHandler : IListPaymentsHandler
{
    public Task<ListPaymentsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listPayments logic
        throw new NotImplementedException("listPayments not implemented");
    }
}