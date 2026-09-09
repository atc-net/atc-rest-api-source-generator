namespace SecurityOpenIdConnect.ApiHandlers;

/// <summary>
/// Handler business logic for the GetCurrentUserProfile operation.
/// </summary>
public sealed class GetCurrentUserProfileHandler : IGetCurrentUserProfileHandler
{
    public Task<GetCurrentUserProfileResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getCurrentUserProfile logic
        throw new NotImplementedException("getCurrentUserProfile not implemented");
    }
}