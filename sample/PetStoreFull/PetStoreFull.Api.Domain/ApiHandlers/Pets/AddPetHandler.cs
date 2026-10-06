namespace PetStoreFull.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the AddPet operation.
/// </summary>
public sealed class AddPetHandler : IAddPetHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public AddPetHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<AddPetResult> ExecuteAsync(
        AddPetParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var pet = repository.AddPet(parameters.Request);

        return Task.FromResult(AddPetResult.Ok(pet));
    }
}