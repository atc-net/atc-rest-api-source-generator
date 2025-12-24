namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the CreateUsersWithListInput operation.
/// </summary>
public sealed class CreateUsersWithListInputHandler : ICreateUsersWithListInputHandler
{
    public Task<CreateUsersWithListInputResult> ExecuteAsync(
        CreateUsersWithListInputParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createUsersWithListInput logic
        throw new NotImplementedException("createUsersWithListInput not implemented");
    }
}