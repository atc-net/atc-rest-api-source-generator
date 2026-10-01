namespace Showcase.BlazorApp.Services;

/// <summary>
/// Gateway service - Documents operations using generated endpoints: raw uploads of several media types, downloads with
/// their own media type and file name, and a summary that is a model or a file.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// List the uploaded documents.
    /// </summary>
    public async Task<List<Document>> ListDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await listDocumentsEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk ? result.OkContent.ToList() : [];
    }

    /// <summary>
    /// Upload a document as the raw request body - sent with the file's own media type (the operation declares
    /// several).
    /// </summary>
    public async Task<Document> UploadDocumentAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var parameters = new UploadDocumentParameters(
            FileName: fileName,
            File: new StreamFileContent(content, fileName, contentType));
        var result = await uploadDocumentEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsCreated
            ? result.CreatedContent
            : throw new HttpRequestException($"Upload failed: {(int)result.StatusCode} {result.Content}");
    }

    /// <summary>
    /// Download a document: its bytes, media type and file name (Content-Disposition).
    /// </summary>
    public async Task<DownloadedFile?> GetDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await getDocumentEndpoint
            .ExecuteAsync(new GetDocumentParameters(DocumentId: documentId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk ? result.OkContent : null;
    }

    /// <summary>
    /// A document's summary - the model, or (printable) the file: the generated result tells them apart by the
    /// response's Content-Type (<c>IsOkFile</c>).
    /// </summary>
    public async Task<(DocumentSummary? Summary, DownloadedFile? File)> GetDocumentSummaryAsync(
        Guid documentId,
        bool printable,
        CancellationToken cancellationToken = default)
    {
        var result = await getDocumentSummaryEndpoint
            .ExecuteAsync(new GetDocumentSummaryParameters(DocumentId: documentId, Printable: printable), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsOk)
        {
            return (null, null);
        }

        return result.IsOkFile ? (null, result.OkFile) : (result.OkContent, null);
    }
}