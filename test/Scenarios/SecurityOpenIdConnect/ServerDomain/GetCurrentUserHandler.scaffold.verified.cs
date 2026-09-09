namespace SecurityOpenIdConnect.ApiHandlers;

/// <summary>
/// Handler business logic for the GetCurrentUser operation.
/// </summary>
public sealed class GetCurrentUserHandler : IGetCurrentUserHandler
{
    public Task<GetCurrentUserResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getCurrentUser logic
        throw new NotImplementedException("getCurrentUser not implemented");
    }
}