namespace Showcase.Api.Domain.ApiHandlers.Documents;

/// <summary>
/// Handler business logic for the UploadDocument operation.
/// Stores the raw request body - a PDF, an Excel workbook, a PNG or a JPEG - with the media type it arrived as (the
/// generated parameters carry it, as the upload declares several).
/// </summary>
public sealed class UploadDocumentHandler : IUploadDocumentHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Documents.UploadDocument");
    private readonly DocumentInMemoryRepository repository;

    public UploadDocumentHandler(DocumentInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<UploadDocumentResult> ExecuteAsync(
        UploadDocumentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("UploadDocument");
        using var buffer = new MemoryStream();
        await parameters.File
            .CopyToAsync(buffer, cancellationToken)
            .ConfigureAwait(false);

        var contentType = parameters.ContentType.Split(';')[0].Trim();
        var stored = repository.Add(parameters.FileName, contentType, buffer.ToArray());
        activity?.SetTag("document.contentType", contentType);
        activity?.SetTag("document.length", stored.Content.LongLength);

        return UploadDocumentResult.Created(DocumentMapper.ToDocument(stored));
    }
}