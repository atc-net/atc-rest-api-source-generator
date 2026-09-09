namespace SecurityOpenIdConnect.ApiHandlers;

/// <summary>
/// Handler business logic for the CreateResource operation.
/// </summary>
public sealed class CreateResourceHandler : ICreateResourceHandler
{
    public Task<CreateResourceResult> ExecuteAsync(
        CreateResourceParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createResource logic
        throw new NotImplementedException("createResource not implemented");
    }
}