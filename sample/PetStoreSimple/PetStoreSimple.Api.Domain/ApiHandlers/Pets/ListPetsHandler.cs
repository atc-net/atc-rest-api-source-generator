namespace PetStoreSimple.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the ListPets operation.
/// </summary>
public sealed class ListPetsHandler : IListPetsHandler
{
    public Task<ListPetsResult> ExecuteAsync(
        ListPetsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listPets logic
        throw new NotImplementedException("listPets not implemented");
    }
}