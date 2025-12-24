namespace Showcase.BlazorApp.Services;

/// <summary>
/// Gateway service - Tasks operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// List all tasks with optional limit.
    /// </summary>
    public async Task<Showcase.Generated.Tasks.Models.Task[]?> ListTasksAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new ListTasksParameters(Limit: limit);
        var result = await listTasksEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

        return result.IsOk
            ? result.OkContent.ToArray()
            : null;
    }

    /// <summary>
    /// Create a new task.
    /// </summary>
    public async Task<Showcase.Generated.Tasks.Models.Task?> CreateTaskAsync(
        Showcase.Generated.Tasks.Models.Task task,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateTaskParameters(Request: task);
        var result = await createTaskEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

        return result.IsCreated
            ? result.CreatedContent
            : null;
    }

    /// <summary>
    /// Get a task by ID.
    /// </summary>
    public async Task<Showcase.Generated.Tasks.Models.Task?> GetTaskByIdAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new GetTaskByIdParameters(TaskId: taskId);
        var result = await getTaskByIdEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

        return result.IsOk
            ? result.OkContent
            : null;
    }

    /// <summary>
    /// Delete a task by ID.
    /// </summary>
    public async Task DeleteTaskByIdAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DeleteTaskByIdParameters(TaskId: taskId);
        var result = await deleteTaskByIdEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

        if (!result.IsNoContent)
        {
            throw new HttpRequestException($"Failed to delete task: {result.StatusCode}");
        }
    }

    /// <summary>
    /// Update a task by ID.
    /// </summary>
    public async Task<Showcase.Generated.Tasks.Models.Task?> UpdateTaskByIdAsync(
        string taskId,
        Showcase.Generated.Tasks.Models.Task task,
        CancellationToken cancellationToken = default)
    {
        var parameters = new UpdateTaskByIdParameters(Request: task, TaskId: taskId);
        var result = await updateTaskByIdEndpoint.ExecuteAsync(parameters, cancellationToken: cancellationToken);

        return result.IsOk
            ? result.OkContent
            : null;
    }
}