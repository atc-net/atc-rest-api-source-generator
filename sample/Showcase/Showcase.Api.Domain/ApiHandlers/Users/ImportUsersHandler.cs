namespace Showcase.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the ImportUsers operation.
/// The list arrives as JSON or as XML - the generated server binds either into the same list by the Content-Type, and
/// the validation filter has checked every item before the handler runs.
/// </summary>
public sealed class ImportUsersHandler : IImportUsersHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Users.ImportUsers");
    private readonly UserInMemoryRepository repository;

    public ImportUsersHandler(UserInMemoryRepository repository)
        => this.repository = repository;

    public async Task<ImportUsersResult> ExecuteAsync(
        ImportUsersParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ImportUsers");
        ArgumentNullException.ThrowIfNull(parameters);

        var users = new List<User>();
        foreach (var request in parameters.Request)
        {
            users.Add(await CreateUserHandler.CreateOrGetAsync(repository, request));
        }

        activity?.SetTag("users.count", users.Count);
        return ImportUsersResult.Ok(users);
    }
}