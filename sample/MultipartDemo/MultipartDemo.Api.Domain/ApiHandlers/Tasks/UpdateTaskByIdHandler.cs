namespace MultipartDemo.Api.Domain.ApiHandlers.Tasks;

/// <summary>
/// Handler business logic for the UpdateTaskById operation.
/// </summary>
public sealed class UpdateTaskByIdHandler : IUpdateTaskByIdHandler
{
    public System.Threading.Tasks.Task<UpdateTaskByIdResult> ExecuteAsync(
        UpdateTaskByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement updateTaskById logic
        throw new NotImplementedException("updateTaskById not implemented");
    }
}