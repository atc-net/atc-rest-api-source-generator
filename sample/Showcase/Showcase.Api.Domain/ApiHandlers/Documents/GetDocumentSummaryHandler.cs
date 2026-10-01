namespace Showcase.Api.Domain.ApiHandlers.Documents;

/// <summary>
/// Handler business logic for the GetDocumentSummary operation.
/// A model or a file: the summary as JSON, or (printable) the same summary as a PDF. Each answer carries an ETag; a
/// request that sends it back as If-None-Match is answered 304 Not Modified while the document is unchanged.
/// </summary>
public sealed class GetDocumentSummaryHandler : IGetDocumentSummaryHandler
{
    private readonly DocumentInMemoryRepository repository;
    private readonly IHttpContextAccessor httpContextAccessor;

    public GetDocumentSummaryHandler(
        DocumentInMemoryRepository repository,
        IHttpContextAccessor httpContextAccessor)
    {
        this.repository = repository;
        this.httpContextAccessor = httpContextAccessor;
    }

    public Task<GetDocumentSummaryResult> ExecuteAsync(
        GetDocumentSummaryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (repository.GetById(parameters.DocumentId) is not { } document)
        {
            return Task.FromResult(GetDocumentSummaryResult.NotFound($"No document {parameters.DocumentId}"));
        }

        // The version of this representation: the document's bytes, and JSON or PDF
        var printable = parameters.Printable == true;
        var etag = $"\"{document.Sha256[..16]}-{(printable ? "pdf" : "json")}\"";
        httpContextAccessor.HttpContext!.Response.Headers.ETag = etag;
        if (string.Equals(parameters.IfNoneMatch, etag, StringComparison.Ordinal))
        {
            return Task.FromResult(GetDocumentSummaryResult.NotModified());
        }

        var summary = DocumentMapper.ToSummary(document);
        if (!printable)
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