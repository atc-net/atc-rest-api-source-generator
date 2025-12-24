namespace PetStoreFull.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the FindPetsByTags operation.
/// </summary>
public sealed class FindPetsByTagsHandler : IFindPetsByTagsHandler
{
    public Task<FindPetsByTagsResult> ExecuteAsync(
        FindPetsByTagsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement findPetsByTags logic
        throw new NotImplementedException("findPetsByTags not implemented");
    }
}