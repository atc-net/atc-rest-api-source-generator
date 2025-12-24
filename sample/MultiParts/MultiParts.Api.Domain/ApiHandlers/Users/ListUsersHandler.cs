namespace MultiParts.Api.Domain.ApiHandlers.Users;

public class ListUsersHandler : IListUsersHandler
{
    private readonly UserInMemoryRepository repository;

    public ListUsersHandler(UserInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<ListUsersResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var users = repository.GetAll();
        return Task.FromResult(ListUsersResult.Ok(users));
    }
}
