namespace Showcase.Api.Domain.ApiHandlers.Files;

/// <summary>
/// Handler business logic for the UploadSingleFileAsFormData operation.
/// Stores a single uploaded file in the in-memory repository.
/// </summary>
public sealed class UploadSingleFileAsFormDataHandler : IUploadSingleFileAsFormDataHandler
{
    private readonly FileInMemoryRepository repository;

    public UploadSingleFileAsFormDataHandler(FileInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<UploadSingleFileAsFormDataResult> ExecuteAsync(
        UploadSingleFileAsFormDataParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (parameters.File is null)
        {
            return UploadSingleFileAsFormDataResult.Ok();
        }

        // For application/octet-stream, the File is a Stream from the request body
        // Metadata like FileName and ContentType must be provided via headers or defaults
        using var memoryStream = new MemoryStream();
        await parameters.File
            .CopyToAsync(memoryStream, cancellationToken)
            .ConfigureAwait(false);

        var content = memoryStream.ToArray();
        const string fileName = "uploaded-file";
        const string contentType = "application/octet-stream";

        await repository
            .SaveAsync(fileName, contentType, content, cancellationToken)
            .ConfigureAwait(false);

        return UploadSingleFileAsFormDataResult.Ok();
    }
}