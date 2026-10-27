namespace Showcase.Api.Domain.ApiHandlers.Documents;

/// <summary>
/// Handler business logic for the ListDocuments operation.
/// </summary>
public sealed class ListDocumentsHandler : IListDocumentsHandler
{
    private readonly DocumentInMemoryRepository repository;

    public ListDocumentsHandler(DocumentInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<ListDocumentsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(ListDocumentsResult.Ok(repository.GetAll().Select(DocumentMapper.ToDocument).ToList()));
}