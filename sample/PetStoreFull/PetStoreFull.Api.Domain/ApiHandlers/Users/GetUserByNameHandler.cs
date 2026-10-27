namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the GetUserByName operation.
/// </summary>
public sealed class GetUserByNameHandler : IGetUserByNameHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public GetUserByNameHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<GetUserByNameResult> ExecuteAsync(
        GetUserByNameParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var user = repository.GetUser(parameters.Username);

        return Task.FromResult(user is null
            ? GetUserByNameResult.NotFound($"User '{parameters.Username}' not found")
            : GetUserByNameResult.Ok(user));
    }
}