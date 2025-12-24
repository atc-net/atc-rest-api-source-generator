namespace MultipartDemo.Api.Domain.ApiHandlers.Testings;

/// <summary>
/// Handler business logic for the GetExceptionTest operation.
/// </summary>
public sealed class GetExceptionTestHandler : IGetExceptionTestHandler
{
    public System.Threading.Tasks.Task<GetExceptionTestResult> ExecuteAsync(
        GetExceptionTestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getExceptionTest logic
        throw new NotImplementedException("getExceptionTest not implemented");
    }
}