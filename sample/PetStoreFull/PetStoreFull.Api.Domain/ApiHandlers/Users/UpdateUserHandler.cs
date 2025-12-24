namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the UpdateUser operation.
/// </summary>
public sealed class UpdateUserHandler : IUpdateUserHandler
{
    public Task<UpdateUserResult> ExecuteAsync(
        UpdateUserParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement updateUser logic
        throw new NotImplementedException("updateUser not implemented");
    }
}