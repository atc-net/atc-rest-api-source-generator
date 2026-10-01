namespace Showcase.Api.Domain.Repositories;

/// <summary>
/// An uploaded document.
/// </summary>
public sealed record StoredDocument(
    Guid Id,
    string FileName,
    string ContentType,
    byte[] Content,
    string Sha256,
    DateTimeOffset UploadedAt);