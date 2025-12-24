namespace MultipartDemo.Api.Domain.ApiHandlers.Tasks;

/// <summary>
/// Handler business logic for the CreateTask operation.
/// </summary>
public sealed class CreateTaskHandler : ICreateTaskHandler
{
    public System.Threading.Tasks.Task<CreateTaskResult> ExecuteAsync(
        CreateTaskParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createTask logic
        throw new NotImplementedException("createTask not implemented");
    }
}