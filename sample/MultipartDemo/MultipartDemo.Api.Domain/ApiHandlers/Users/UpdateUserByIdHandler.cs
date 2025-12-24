namespace MultipartDemo.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the UpdateUserById operation.
/// </summary>
public sealed class UpdateUserByIdHandler : IUpdateUserByIdHandler
{
    public System.Threading.Tasks.Task<UpdateUserByIdResult> ExecuteAsync(
        UpdateUserByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement updateUserById logic
        throw new NotImplementedException("updateUserById not implemented");
    }
}