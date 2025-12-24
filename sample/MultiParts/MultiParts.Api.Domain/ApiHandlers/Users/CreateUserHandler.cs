namespace MultiParts.Api.Domain.ApiHandlers.Users;

public class CreateUserHandler : ICreateUserHandler
{
    private readonly UserInMemoryRepository repository;

    public CreateUserHandler(UserInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<CreateUserResult> ExecuteAsync(
        CreateUserParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.Request);

        var newUser = repository.Add(
            parameters.Request.Name,
            parameters.Request.Email);

        return Task.FromResult(CreateUserResult.Created(newUser));
    }
}
