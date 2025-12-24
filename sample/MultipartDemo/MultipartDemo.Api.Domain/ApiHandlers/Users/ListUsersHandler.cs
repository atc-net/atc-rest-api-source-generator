namespace MultipartDemo.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the ListUsers operation.
/// </summary>
public sealed class ListUsersHandler : IListUsersHandler
{
    public System.Threading.Tasks.Task<ListUsersResult> ExecuteAsync(
        ListUsersParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listUsers logic
        throw new NotImplementedException("listUsers not implemented");
    }
}