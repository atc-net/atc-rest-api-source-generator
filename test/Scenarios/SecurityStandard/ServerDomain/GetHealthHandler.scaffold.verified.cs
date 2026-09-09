namespace SecurityStandard.ApiHandlers;

/// <summary>
/// Handler business logic for the GetHealth operation.
/// </summary>
public sealed class GetHealthHandler : IGetHealthHandler
{
    public Task<GetHealthResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getHealth logic
        throw new NotImplementedException("getHealth not implemented");
    }
}