namespace Retry.ApiHandlers;

/// <summary>
/// Handler business logic for the GetHealth operation.
/// </summary>
public sealed class GetHealthHandler : IGetHealthHandler
{
    /// <summary>
    /// Executes the GetHealth operation.
    /// </summary>
    public Task<GetHealthResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getHealth logic
        throw new NotImplementedException("getHealth not implemented");
    }
}