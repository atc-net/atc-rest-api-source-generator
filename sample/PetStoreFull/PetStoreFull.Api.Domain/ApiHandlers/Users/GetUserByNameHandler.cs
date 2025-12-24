namespace PetStoreFull.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the GetUserByName operation.
/// </summary>
public sealed class GetUserByNameHandler : IGetUserByNameHandler
{
    public Task<GetUserByNameResult> ExecuteAsync(
        GetUserByNameParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getUserByName logic
        throw new NotImplementedException("getUserByName not implemented");
    }
}