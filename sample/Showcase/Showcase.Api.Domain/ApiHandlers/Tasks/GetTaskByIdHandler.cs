namespace Showcase.Api.Domain.ApiHandlers.Tasks;

/// <summary>
/// Handler business logic for the GetTaskById operation.
/// </summary>
public sealed class GetTaskByIdHandler : IGetTaskByIdHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Tasks.GetTaskById");
    private readonly TaskInMemoryRepository repository;

    public GetTaskByIdHandler(TaskInMemoryRepository repository)
        => this.repository = repository;

    public async Task<GetTaskByIdResult> ExecuteAsync(
        GetTaskByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GetTaskById");
        var task = await repository.GetById(parameters.TaskId);

        return task is null
            ? GetTaskByIdResult.NotFound()
            : GetTaskByIdResult.Ok(task);
    }
}