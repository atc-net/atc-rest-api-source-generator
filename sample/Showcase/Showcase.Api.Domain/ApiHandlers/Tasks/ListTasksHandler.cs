namespace Showcase.Api.Domain.ApiHandlers.Tasks;

/// <summary>
/// Handler business logic for the ListTasks operation.
/// </summary>
public sealed class ListTasksHandler : IListTasksHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Tasks.ListTasks");
    private readonly TaskInMemoryRepository repository;

    public ListTasksHandler(TaskInMemoryRepository repository)
        => this.repository = repository;

    public async Task<ListTasksResult> ExecuteAsync(
        ListTasksParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ListTasks");
        var tasks = await repository.GetAll(parameters.Limit);
        return ListTasksResult.Ok(tasks);
    }
}