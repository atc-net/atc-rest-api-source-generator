namespace Showcase.Api.Domain.ApiHandlers.Files;

/// <summary>
/// Handler business logic for the UploadSingleObjectWithFilesAsFormData operation.
/// Stores multiple files from a form data request in the in-memory repository.
/// </summary>
public sealed class UploadSingleObjectWithFilesAsFormDataHandler : IUploadSingleObjectWithFilesAsFormDataHandler
{
    private readonly FileInMemoryRepository repository;

    public UploadSingleObjectWithFilesAsFormDataHandler(
        FileInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<UploadSingleObjectWithFilesAsFormDataResult> ExecuteAsync(
        UploadSingleObjectWithFilesAsFormDataParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (parameters.Request?.Files is null || parameters.Request.Files.Count == 0)
        {
            return UploadSingleObjectWithFilesAsFormDataResult.Ok();
        }

        var fileInfos = new List<(string FileName, string ContentType, byte[] Content)>();
        var fileIndex = 0;

        foreach (var file in parameters.Request.Files)
        {
            using var memoryStream = new MemoryStream();
            await file
                .CopyToAsync(memoryStream, cancellationToken)
                .ConfigureAwait(false);

            var content = memoryStream.ToArray();
            var fileName = file.FileName ?? $"multi-upload-{fileIndex + 1}";
            var contentType = file.ContentType ?? "application/octet-stream";

            fileInfos.Add((fileName, contentType, content));
            fileIndex++;
        }

        await repository
            .SaveMultipleAsync(fileInfos, cancellationToken)
            .ConfigureAwait(false);

        return UploadSingleObjectWithFilesAsFormDataResult.Ok();
    }
}