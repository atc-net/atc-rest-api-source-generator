namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the CreateUser operation.
/// </summary>
public sealed class CreateUserHandler : ICreateUserHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public CreateUserHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<CreateUserResult> ExecuteAsync(
        CreateUserParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // The specification declares only the default response for errors
        if (string.IsNullOrWhiteSpace(parameters.Request.Username))
        {
            return Task.FromResult(CreateUserResult.Error("A username is required", StatusCodes.Status400BadRequest));
        }

        var user = repository.AddUser(parameters.Request);

        return Task.FromResult(CreateUserResult.Ok(user));
    }
}