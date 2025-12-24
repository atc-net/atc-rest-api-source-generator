namespace MultipartDemo.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the DeleteUserById operation.
/// </summary>
public sealed class DeleteUserByIdHandler : IDeleteUserByIdHandler
{
    public System.Threading.Tasks.Task<DeleteUserByIdResult> ExecuteAsync(
        DeleteUserByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement deleteUserById logic
        throw new NotImplementedException("deleteUserById not implemented");
    }
}