namespace Showcase.Api.Domain.ApiHandlers.Files;

/// <summary>
/// Handler business logic for the UploadMultiFilesAsFormData operation.
/// Stores multiple uploaded files in the in-memory repository.
/// </summary>
public sealed class UploadMultiFilesAsFormDataHandler : IUploadMultiFilesAsFormDataHandler
{
    private readonly FileInMemoryRepository repository;

    public UploadMultiFilesAsFormDataHandler(FileInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<UploadMultiFilesAsFormDataResult> ExecuteAsync(
        UploadMultiFilesAsFormDataParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (parameters.File is null || parameters.File.Count == 0)
        {
            return UploadMultiFilesAsFormDataResult.Ok();
        }

        var fileInfos = new List<(string FileName, string ContentType, byte[] Content)>();

        foreach (var file in parameters.File)
        {
            using var memoryStream = new MemoryStream();
            await file
                .CopyToAsync(memoryStream, cancellationToken)
                .ConfigureAwait(false);

            var content = memoryStream.ToArray();
            var fileName = file.FileName ?? $"uploaded-file-{fileInfos.Count + 1}";
            var contentType = file.ContentType ?? "application/octet-stream";

            fileInfos.Add((fileName, contentType, content));
        }

        await repository
            .SaveMultipleAsync(fileInfos, cancellationToken)
            .ConfigureAwait(false);

        return UploadMultiFilesAsFormDataResult.Ok();
    }
}