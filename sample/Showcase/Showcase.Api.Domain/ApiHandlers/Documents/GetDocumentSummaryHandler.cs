namespace Showcase.Api.Domain.ApiHandlers.Documents;

/// <summary>
/// Handler business logic for the GetDocumentSummary operation.
/// A model or a file: the summary as JSON, or (printable) the same summary as a PDF.
/// </summary>
public sealed class GetDocumentSummaryHandler : IGetDocumentSummaryHandler
{
    private readonly DocumentInMemoryRepository repository;

    public GetDocumentSummaryHandler(DocumentInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<GetDocumentSummaryResult> ExecuteAsync(
        GetDocumentSummaryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (repository.GetById(parameters.DocumentId) is not { } document)
        {
            return Task.FromResult(GetDocumentSummaryResult.NotFound($"No document {parameters.DocumentId}"));
        }

        var summary = DocumentMapper.ToSummary(document);
        if (parameters.Printable != true)
        {
            return Task.FromResult(GetDocumentSummaryResult.Ok(summary));
        }

        var pdf = SimplePdf.Create(
            "Document summary",
            [
                $"File name: {summary.FileName}",
                $"Kind: {summary.Kind}",
                $"Media type: {summary.ContentType}",
                $"Length: {summary.Length.ToString("N0", CultureInfo.InvariantCulture)} bytes",
                $"SHA-256: {summary.Sha256}",
            ]);
        return Task.FromResult(GetDocumentSummaryResult.Ok(pdf, "application/pdf", $"{Path.GetFileNameWithoutExtension(summary.FileName)}-summary.pdf"));
    }
}