namespace Showcase.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the DeleteUserById operation.
/// </summary>
public sealed class DeleteUserByIdHandler : IDeleteUserByIdHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Users.DeleteUserById");
    private readonly UserInMemoryRepository repository;

    public DeleteUserByIdHandler(UserInMemoryRepository repository)
        => this.repository = repository;

    public async Task<DeleteUserByIdResult> ExecuteAsync(
        DeleteUserByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("DeleteUserById");
        ArgumentNullException.ThrowIfNull(parameters);

        var user = await repository.Delete(parameters.UserId);
        if (user is null)
        {
            return DeleteUserByIdResult.NotFound();
        }

        return DeleteUserByIdResult.NoContent();
    }
}