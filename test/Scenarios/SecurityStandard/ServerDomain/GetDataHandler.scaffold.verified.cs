namespace SecurityStandard.ApiHandlers;

/// <summary>
/// Handler business logic for the GetData operation.
/// </summary>
public sealed class GetDataHandler : IGetDataHandler
{
    public Task<GetDataResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getData logic
        throw new NotImplementedException("getData not implemented");
    }
}