namespace PetStoreFull.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the FindPetsByTags operation.
/// </summary>
public sealed class FindPetsByTagsHandler : IFindPetsByTagsHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public FindPetsByTagsHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<FindPetsByTagsResult> ExecuteAsync(
        FindPetsByTagsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var pets = repository.FindPetsByTags(parameters.Tags);

        return Task.FromResult(FindPetsByTagsResult.Ok(pets));
    }
}