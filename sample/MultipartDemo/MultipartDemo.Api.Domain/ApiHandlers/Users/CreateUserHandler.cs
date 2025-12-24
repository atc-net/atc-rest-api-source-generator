namespace MultipartDemo.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the CreateUser operation.
/// </summary>
public sealed class CreateUserHandler : ICreateUserHandler
{
    public System.Threading.Tasks.Task<CreateUserResult> ExecuteAsync(
        CreateUserParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createUser logic
        throw new NotImplementedException("createUser not implemented");
    }
}