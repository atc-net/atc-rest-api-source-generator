namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the UpdateUser operation.
/// </summary>
public sealed class UpdateUserHandler : IUpdateUserHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public UpdateUserHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<UpdateUserResult> ExecuteAsync(
        UpdateUserParameters parameters,
        CancellationToken cancellationToken = default)
        => Task.FromResult(repository.UpdateUser(parameters.Username, parameters.Request) is null
            ? UpdateUserResult.NotFound($"User '{parameters.Username}' not found")
            : UpdateUserResult.Ok());
}