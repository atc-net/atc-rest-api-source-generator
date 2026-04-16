namespace Showcase.Api.Domain.ApiHandlers.Tasks;

/// <summary>
/// Handler business logic for the DeleteTaskById operation.
/// </summary>
public sealed class DeleteTaskByIdHandler : IDeleteTaskByIdHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Tasks.DeleteTaskById");
    private readonly TaskInMemoryRepository repository;

    public DeleteTaskByIdHandler(TaskInMemoryRepository repository)
        => this.repository = repository;

    public async Task<DeleteTaskByIdResult> ExecuteAsync(
        DeleteTaskByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("DeleteTaskById");
        var task = await repository.Delete(parameters.TaskId);

        return task is null
            ? DeleteTaskByIdResult.NotFound()
            : DeleteTaskByIdResult.NoContent();
    }
}