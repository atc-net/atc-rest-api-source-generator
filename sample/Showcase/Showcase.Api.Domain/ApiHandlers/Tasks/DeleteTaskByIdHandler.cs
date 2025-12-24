namespace Showcase.Api.Domain.ApiHandlers.Tasks;

/// <summary>
/// Handler business logic for the DeleteTaskById operation.
/// </summary>
public sealed class DeleteTaskByIdHandler : IDeleteTaskByIdHandler
{
    private readonly TaskInMemoryRepository repository;

    public DeleteTaskByIdHandler(TaskInMemoryRepository repository)
        => this.repository = repository;

    public async Task<DeleteTaskByIdResult> ExecuteAsync(
        DeleteTaskByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var task = await repository.Delete(parameters.TaskId);

        return task is null
            ? DeleteTaskByIdResult.NotFound()
            : DeleteTaskByIdResult.NoContent();
    }
}