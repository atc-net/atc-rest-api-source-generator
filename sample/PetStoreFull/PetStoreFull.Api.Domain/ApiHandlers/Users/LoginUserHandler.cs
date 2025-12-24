namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the LoginUser operation.
/// </summary>
public sealed class LoginUserHandler : ILoginUserHandler
{
    public Task<LoginUserResult> ExecuteAsync(
        LoginUserParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement loginUser logic
        throw new NotImplementedException("loginUser not implemented");
    }
}