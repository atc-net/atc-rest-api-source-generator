namespace SecurityOpenIdConnect.ApiHandlers;

/// <summary>
/// Handler business logic for the DeleteResource operation.
/// </summary>
public sealed class DeleteResourceHandler : IDeleteResourceHandler
{
    public Task<DeleteResourceResult> ExecuteAsync(
        DeleteResourceParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement deleteResource logic
        throw new NotImplementedException("deleteResource not implemented");
    }
}