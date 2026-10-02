namespace PetStoreFull.ApiHandlers;

/// <summary>
/// Handler business logic for the LogoutUser operation.
/// </summary>
public sealed class LogoutUserHandler : ILogoutUserHandler
{
    /// <summary>
    /// Executes the LogoutUser operation.
    /// </summary>
    public Task<LogoutUserResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement logoutUser logic
        throw new NotImplementedException("logoutUser not implemented");
    }
}