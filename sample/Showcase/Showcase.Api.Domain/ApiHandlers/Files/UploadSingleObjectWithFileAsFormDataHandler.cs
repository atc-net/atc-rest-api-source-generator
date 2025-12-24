namespace Showcase.Api.Domain.ApiHandlers.Files;

/// <summary>
/// Handler business logic for the UploadSingleObjectWithFileAsFormData operation.
/// Stores a file with associated metadata in the in-memory repository.
/// </summary>
public sealed class UploadSingleObjectWithFileAsFormDataHandler : IUploadSingleObjectWithFileAsFormDataHandler
{
    private readonly FileInMemoryRepository repository;

    public UploadSingleObjectWithFileAsFormDataHandler(
        FileInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<UploadSingleObjectWithFileAsFormDataResult> ExecuteAsync(
        UploadSingleObjectWithFileAsFormDataParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (parameters.Request is null)
        {
            return UploadSingleObjectWithFileAsFormDataResult.Ok();
        }

        var request = parameters.Request;

        // Log the metadata (in a real app, you'd store this too)
        var itemName = request.ItemName;
        var items = request.Items;

        // Store the file if present
        if (request.File is not null)
        {
            using var memoryStream = new MemoryStream();
            await request.File
                .CopyToAsync(memoryStream, cancellationToken)
                .ConfigureAwait(false);

            var content = memoryStream.ToArray();

            // Use ItemName as base for filename, with tags info
            var fileName = $"{itemName}-{string.Join("-", items)}.bin";

            await repository
                .SaveAsync(fileName, "application/octet-stream", content, cancellationToken)
                .ConfigureAwait(false);
        }

        return UploadSingleObjectWithFileAsFormDataResult.Ok();
    }
}