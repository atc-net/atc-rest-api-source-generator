namespace Showcase.Api.Domain.ApiHandlers.Documents;

/// <summary>
/// Maps a stored document to the API's models.
/// </summary>
internal static class DocumentMapper
{
    public static Document ToDocument(StoredDocument document)
        => new(
            Id: document.Id,
            FileName: document.FileName,
            ContentType: document.ContentType,
            Length: document.Content.LongLength,
            Sha256: document.Sha256,
            UploadedAt: document.UploadedAt);

    public static DocumentSummary ToSummary(StoredDocument document)
        => new(
            Id: document.Id,
            FileName: document.FileName,
            Kind: KindOf(document.ContentType),
            ContentType: document.ContentType,
            Length: document.Content.LongLength,
            Sha256: document.Sha256);

    private static string KindOf(string contentType)
        => contentType switch
        {
            "application/pdf" => "pdf",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => "spreadsheet",
            _ when contentType.StartsWith("image/", StringComparison.Ordinal) => "image",
            _ => "other",
        };
}