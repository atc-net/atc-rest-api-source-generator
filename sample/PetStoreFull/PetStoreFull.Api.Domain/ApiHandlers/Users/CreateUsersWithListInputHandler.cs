namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the CreateUsersWithListInput operation.
/// </summary>
public sealed class CreateUsersWithListInputHandler : ICreateUsersWithListInputHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public CreateUsersWithListInputHandler(
        PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<CreateUsersWithListInputResult> ExecuteAsync(
        CreateUsersWithListInputParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // The specification declares only the default response for errors
        if (parameters.Request.Count == 0 || parameters.Request.Any(user => string.IsNullOrWhiteSpace(user.Username)))
        {
            return Task.FromResult(CreateUsersWithListInputResult.Error("Every user needs a username", StatusCodes.Status400BadRequest));
        }

        // The specification answers with one user: the last one created
        var created = parameters.Request
            .Select(repository.AddUser)
            .Last();

        return Task.FromResult(CreateUsersWithListInputResult.Ok(created));
    }
}