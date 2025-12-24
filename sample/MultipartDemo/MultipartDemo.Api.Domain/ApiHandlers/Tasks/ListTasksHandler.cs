namespace MultipartDemo.Api.Domain.ApiHandlers.Tasks;

/// <summary>
/// Handler business logic for the ListTasks operation.
/// </summary>
public sealed class ListTasksHandler : IListTasksHandler
{
    public System.Threading.Tasks.Task<ListTasksResult> ExecuteAsync(
        ListTasksParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listTasks logic
        throw new NotImplementedException("listTasks not implemented");
    }
}