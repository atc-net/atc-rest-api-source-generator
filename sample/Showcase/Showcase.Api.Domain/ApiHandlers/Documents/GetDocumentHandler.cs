namespace Showcase.Api.Domain.ApiHandlers.Documents;

/// <summary>
/// Handler business logic for the GetDocument operation.
/// Answers the document with its own media type and file name (Content-Disposition, <c>filename*</c> for a
/// non-ASCII name).
/// </summary>
public sealed class GetDocumentHandler : IGetDocumentHandler
{
    private readonly DocumentInMemoryRepository repository;

    public GetDocumentHandler(DocumentInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<GetDocumentResult> ExecuteAsync(
        GetDocumentParameters parameters,
        CancellationToken cancellationToken = default)
        => Task.FromResult(repository.GetById(parameters.DocumentId) is { } document
            ? GetDocumentResult.Ok(document.Content, document.ContentType, document.FileName)
            : GetDocumentResult.NotFound($"No document {parameters.DocumentId}"));
}