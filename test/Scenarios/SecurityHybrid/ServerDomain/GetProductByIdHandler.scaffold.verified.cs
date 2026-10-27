namespace SecurityHybrid.ApiHandlers;

/// <summary>
/// Handler business logic for the GetProductById operation.
/// </summary>
public sealed class GetProductByIdHandler : IGetProductByIdHandler
{
    /// <summary>
    /// Executes the GetProductById operation.
    /// </summary>
    public Task<GetProductByIdResult> ExecuteAsync(
        GetProductByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getProductById logic
        throw new NotImplementedException("getProductById not implemented");
    }
}