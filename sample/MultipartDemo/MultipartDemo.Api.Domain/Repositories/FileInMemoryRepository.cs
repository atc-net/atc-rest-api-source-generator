namespace MultipartDemo.Api.Domain.Repositories;

public sealed class FileInMemoryRepository
{
    private readonly ConcurrentDictionary<Guid, StoredFile> files = new();

    public FileInMemoryRepository()
    {
        // Add a sample file
        var id = Guid.NewGuid();
        files[id] = new StoredFile
        {
            Id = id,
            FileName = "sample.txt",
            ContentType = "text/plain",
            Content = System.Text.Encoding.UTF8.GetBytes("Hello, World!"),
            Description = "A sample text file",
            CreatedAt = DateTime.UtcNow,
        };
    }

    public Task<StoredFile?> GetByIdAsync(Guid id)
    {
        files.TryGetValue(id, out var file);
        return Task.FromResult(file);
    }

    public Task<StoredFile> AddAsync(
        string fileName,
        string contentType,
        byte[] content,
        string? description = null)
    {
        var file = new StoredFile
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = contentType,
            Content = content,
            Description = description,
            CreatedAt = DateTime.UtcNow,
        };
        files[file.Id] = file;
        return Task.FromResult(file);
    }

    public Task<IReadOnlyList<StoredFile>> AddMultipleAsync(
        IEnumerable<(string FileName, string ContentType, byte[] Content)> fileInfos)
    {
        var addedFiles = new List<StoredFile>();
        foreach (var (fileName, contentType, content) in fileInfos)
        {
            var file = new StoredFile
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                ContentType = contentType,
                Content = content,
                CreatedAt = DateTime.UtcNow,
            };
            files[file.Id] = file;
            addedFiles.Add(file);
        }

        return Task.FromResult<IReadOnlyList<StoredFile>>(addedFiles);
    }

    public Task<bool> DeleteAsync(Guid id)
        => Task.FromResult(files.TryRemove(id, out _));
}