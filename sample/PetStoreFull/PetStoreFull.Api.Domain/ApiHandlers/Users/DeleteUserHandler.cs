namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the DeleteUser operation.
/// </summary>
public sealed class DeleteUserHandler : IDeleteUserHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public DeleteUserHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<DeleteUserResult> ExecuteAsync(
        DeleteUserParameters parameters,
        CancellationToken cancellationToken = default)
        => Task.FromResult(repository.RemoveUser(parameters.Username)
            ? DeleteUserResult.Ok()
            : DeleteUserResult.NotFound($"User '{parameters.Username}' not found"));
}