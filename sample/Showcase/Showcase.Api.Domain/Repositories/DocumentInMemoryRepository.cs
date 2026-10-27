namespace Showcase.Api.Domain.Repositories;

/// <summary>
/// In-memory store of uploaded documents (PDF, Excel, PNG, JPEG) - their bytes, media type and file name.
/// </summary>
public sealed class DocumentInMemoryRepository
{
    private readonly ConcurrentDictionary<Guid, StoredDocument> documents = new();

    public IReadOnlyList<StoredDocument> GetAll()
        => documents.Values
            .OrderByDescending(d => d.UploadedAt)
            .ToList();

    public StoredDocument? GetById(Guid id)
        => documents.GetValueOrDefault(id);

    public StoredDocument Add(
        string fileName,
        string contentType,
        byte[] content)
    {
        var document = new StoredDocument(
            Guid.NewGuid(),
            fileName,
            contentType,
            content,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            DateTimeOffset.UtcNow);
        documents[document.Id] = document;
        return document;
    }
}