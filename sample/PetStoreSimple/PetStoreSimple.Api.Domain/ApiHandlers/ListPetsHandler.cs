namespace PetStoreSimple.Api.Domain.ApiHandlers;

public class ListPetsHandler : IListPetsHandler
{
    private readonly PetInMemoryRepository repository;

    public ListPetsHandler(PetInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<ListPetsResult> ExecuteAsync(
        ListPetsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var pets = repository.GetAll(parameters.Limit);
        return Task.FromResult(ListPetsResult.Ok(pets));
    }
}