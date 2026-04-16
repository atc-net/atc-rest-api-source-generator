namespace Showcase.Api.Domain.ApiHandlers.Tasks;

/// <summary>
/// Handler business logic for the CreateTask operation.
/// </summary>
public sealed class CreateTaskHandler : ICreateTaskHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Tasks.CreateTask");
    private readonly TaskInMemoryRepository repository;

    public CreateTaskHandler(TaskInMemoryRepository repository)
        => this.repository = repository;

    public async Task<CreateTaskResult> ExecuteAsync(
        CreateTaskParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("CreateTask");
        ArgumentNullException.ThrowIfNull(parameters);

        var task = await repository.Create(
            parameters.Request.Id,
            parameters.Request.Name,
            parameters.Request.Tag);

        return CreateTaskResult.Created(task);
    }
}