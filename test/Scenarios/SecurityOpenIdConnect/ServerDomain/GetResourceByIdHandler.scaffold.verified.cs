namespace SecurityOpenIdConnect.ApiHandlers;

/// <summary>
/// Handler business logic for the GetResourceById operation.
/// </summary>
public sealed class GetResourceByIdHandler : IGetResourceByIdHandler
{
    /// <summary>
    /// Executes the GetResourceById operation.
    /// </summary>
    public Task<GetResourceByIdResult> ExecuteAsync(
        GetResourceByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getResourceById logic
        throw new NotImplementedException("getResourceById not implemented");
    }
}