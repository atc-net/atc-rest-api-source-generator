namespace Showcase.Api.Domain.ApiHandlers.Tasks;

/// <summary>
/// Handler business logic for the UpdateTaskById operation.
/// </summary>
public sealed class UpdateTaskByIdHandler : IUpdateTaskByIdHandler
{
    private readonly TaskInMemoryRepository repository;

    public UpdateTaskByIdHandler(TaskInMemoryRepository repository)
        => this.repository = repository;

    public async Task<UpdateTaskByIdResult> ExecuteAsync(
        UpdateTaskByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var task = await repository.Update(
            parameters.TaskId,
            parameters.Request.Name,
            parameters.Request.Tag);

        return task is null
            ? UpdateTaskByIdResult.NotFound()
            : UpdateTaskByIdResult.Ok(task);
    }
}