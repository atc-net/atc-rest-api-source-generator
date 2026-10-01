namespace Showcase.Api.Domain.ApiHandlers.Documents;

/// <summary>
/// Handler business logic for the CopyDocument operation.
/// Post / redirect / get: the copy is made, and the answer is 303 See Other to the copy's summary, which a client that
/// follows redirects reads with a GET.
/// </summary>
public sealed class CopyDocumentHandler : ICopyDocumentHandler
{
    private readonly DocumentInMemoryRepository repository;

    public CopyDocumentHandler(DocumentInMemoryRepository repository)
        => this.repository = repository;

    public Task<CopyDocumentResult> ExecuteAsync(
        CopyDocumentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (repository.GetById(parameters.DocumentId) is not { } document)
        {
            return Task.FromResult(CopyDocumentResult.NotFound($"No document {parameters.DocumentId}"));
        }

        var copy = repository.Add($"Copy of {document.FileName}", document.ContentType, document.Content);
        return Task.FromResult(CopyDocumentResult.SeeOther(new Uri($"/api/v1/documents/{copy.Id}/summary", UriKind.Relative)));
    }
}