namespace PetStoreSimple.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the ListPets operation.
/// </summary>
public sealed class ListPetsHandler : IListPetsHandler
{
    private readonly PetInMemoryRepository repository;

    public ListPetsHandler(PetInMemoryRepository repository)
        => this.repository = repository;

    public Task<ListPetsResult> ExecuteAsync(
        ListPetsParameters parameters,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ListPetsResult.Ok(repository.GetAll(parameters.Limit)));
}