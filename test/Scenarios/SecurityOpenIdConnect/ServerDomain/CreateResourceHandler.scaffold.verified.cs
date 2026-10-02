namespace SecurityOpenIdConnect.ApiHandlers;

/// <summary>
/// Handler business logic for the CreateResource operation.
/// </summary>
public sealed class CreateResourceHandler : ICreateResourceHandler
{
    /// <summary>
    /// Executes the CreateResource operation.
    /// </summary>
    public Task<CreateResourceResult> ExecuteAsync(
        CreateResourceParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createResource logic
        throw new NotImplementedException("createResource not implemented");
    }
}