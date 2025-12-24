namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the DeleteUser operation.
/// </summary>
public sealed class DeleteUserHandler : IDeleteUserHandler
{
    public Task<DeleteUserResult> ExecuteAsync(
        DeleteUserParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement deleteUser logic
        throw new NotImplementedException("deleteUser not implemented");
    }
}