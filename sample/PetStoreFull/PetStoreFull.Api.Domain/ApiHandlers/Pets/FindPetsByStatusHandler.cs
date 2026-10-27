namespace PetStoreFull.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the FindPetsByStatus operation.
/// </summary>
public sealed class FindPetsByStatusHandler : IFindPetsByStatusHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public FindPetsByStatusHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<FindPetsByStatusResult> ExecuteAsync(
        FindPetsByStatusParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var pets = repository.FindPetsByStatus(parameters.Status.ToWireValue());

        return Task.FromResult(FindPetsByStatusResult.Ok(pets));
    }
}