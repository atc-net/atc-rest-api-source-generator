namespace MultipartDemo.Api.Domain.ApiHandlers.Tasks;

/// <summary>
/// Handler business logic for the DeleteTaskById operation.
/// </summary>
public sealed class DeleteTaskByIdHandler : IDeleteTaskByIdHandler
{
    public System.Threading.Tasks.Task<DeleteTaskByIdResult> ExecuteAsync(
        DeleteTaskByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement deleteTaskById logic
        throw new NotImplementedException("deleteTaskById not implemented");
    }
}