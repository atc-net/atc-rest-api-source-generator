namespace PetStoreFull.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the AddPet operation.
/// </summary>
public sealed class AddPetHandler : IAddPetHandler
{
    public Task<AddPetResult> ExecuteAsync(
        AddPetParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement addPet logic
        throw new NotImplementedException("addPet not implemented");
    }
}