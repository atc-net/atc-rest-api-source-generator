namespace Showcase.Api.Domain.Repositories;

/// <summary>
/// Represents a file stored in the repository.
/// </summary>
public sealed class StoredFile
{
    public StoredFile(
        string id,
        string fileName,
        string contentType,
        ReadOnlyMemory<byte> content,
        DateTimeOffset uploadedAt)
    {
        Id = id;
        FileName = fileName;
        ContentType = contentType;
        Content = content;
        UploadedAt = uploadedAt;
    }

    public string Id { get; }

    public string FileName { get; }

    public string ContentType { get; }

    public ReadOnlyMemory<byte> Content { get; }

    public DateTimeOffset UploadedAt { get; }

    public long Size => Content.Length;
}