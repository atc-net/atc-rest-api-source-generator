namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the LoginUser operation.
/// </summary>
public sealed class LoginUserHandler : ILoginUserHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public LoginUserHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<LoginUserResult> ExecuteAsync(
        LoginUserParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var user = parameters.Username is null
            ? null
            : repository.GetUser(parameters.Username);

        if (user is null || !string.Equals(user.Password, parameters.Password, StringComparison.Ordinal))
        {
            return Task.FromResult(LoginUserResult.BadRequest());
        }

        // X-Rate-Limit and X-Expires-After are headers the 200 declares: the factory writes them
        return Task.FromResult(LoginUserResult.Ok(
            $"logged in user session:{Guid.NewGuid():N}",
            xRateLimit: 5000,
            xExpiresAfter: DateTimeOffset.UtcNow.AddHours(1)));
    }
}