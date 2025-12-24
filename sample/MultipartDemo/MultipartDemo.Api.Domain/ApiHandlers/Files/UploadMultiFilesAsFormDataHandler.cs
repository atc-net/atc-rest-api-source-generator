namespace MultipartDemo.Api.Domain.ApiHandlers.Files;

/// <summary>
/// Handler business logic for the UploadMultiFilesAsFormData operation.
/// </summary>
public sealed class UploadMultiFilesAsFormDataHandler : IUploadMultiFilesAsFormDataHandler
{
    public System.Threading.Tasks.Task<UploadMultiFilesAsFormDataResult> ExecuteAsync(
        UploadMultiFilesAsFormDataParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement uploadMultiFilesAsFormData logic
        throw new NotImplementedException("uploadMultiFilesAsFormData not implemented");
    }
}