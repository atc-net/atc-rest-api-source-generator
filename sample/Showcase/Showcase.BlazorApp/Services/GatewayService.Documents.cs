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
            .ExecuteAsync(new GetDocumentSummaryParameters(DocumentId: documentId, IfNoneMatch: null, Printable: printable), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsOk)
        {
            return (null, null);
        }

        return result.IsOkFile ? (null, result.OkFile) : (result.OkContent, null);
    }

    /// <summary>
    /// A document's summary as JSON with its ETag - or, when the ETag sent as If-None-Match still matches, 304 Not
    /// Modified (the generated result's <c>IsNotModified</c>, never an error).
    /// </summary>
    public async Task<(DocumentSummary? Summary, string? ETag, bool NotModified)> GetDocumentSummaryJsonAsync(
        Guid documentId,
        string? ifNoneMatch,
        CancellationToken cancellationToken = default)
    {
        var result = await getDocumentSummaryEndpoint
            .ExecuteAsync(new GetDocumentSummaryParameters(DocumentId: documentId, IfNoneMatch: ifNoneMatch, Printable: false), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var etag = result.Headers.TryGetValue("ETag", out var values) ? values.FirstOrDefault() : null;
        if (result.IsNotModified)
        {
            return (null, etag, true);
        }

        return result is { IsOk: true, IsOkFile: false } ? (result.OkContent, etag, false) : (null, null, false);
    }

    /// <summary>
    /// Copy a document. The API answers 303 See Other to the copy's summary; the browser's HttpClient follows it, so
    /// the answer is the summary's 200 - a status the copy operation does not declare, so its body is in
    /// <c>Content</c>.
    /// </summary>
    public async Task<DocumentSummary?> CopyDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await copyDocumentEndpoint
            .ExecuteAsync(new CopyDocumentParameters(DocumentId: documentId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess && !string.IsNullOrEmpty(result.Content)
            ? JsonSerializer.Deserialize<DocumentSummary>(result.Content, jsonOptions)
            : null;
    }
}