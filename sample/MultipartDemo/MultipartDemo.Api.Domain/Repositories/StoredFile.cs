namespace MultipartDemo.Api.Domain.Repositories;

public sealed class StoredFile
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public byte[] Content { get; set; } = [];

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}