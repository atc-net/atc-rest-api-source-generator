namespace SecurityStandard.ApiHandlers;

/// <summary>
/// Handler business logic for the ListUsers operation.
/// </summary>
public sealed class ListUsersHandler : IListUsersHandler
{
    public Task<ListUsersResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listUsers logic
        throw new NotImplementedException("listUsers not implemented");
    }
}