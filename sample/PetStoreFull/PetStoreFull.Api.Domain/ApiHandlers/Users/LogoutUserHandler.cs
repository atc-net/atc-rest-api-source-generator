namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the LogoutUser operation.
/// </summary>
public sealed class LogoutUserHandler : ILogoutUserHandler
{
    public Task<LogoutUserResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(LogoutUserResult.Ok());
}