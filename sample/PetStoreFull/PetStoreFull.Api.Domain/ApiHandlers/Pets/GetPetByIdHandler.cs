namespace PetStoreFull.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the GetPetById operation.
/// </summary>
public sealed class GetPetByIdHandler : IGetPetByIdHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public GetPetByIdHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<GetPetByIdResult> ExecuteAsync(
        GetPetByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var pet = repository.GetPet(parameters.PetId);

        return Task.FromResult(pet is null
            ? GetPetByIdResult.NotFound($"Pet {parameters.PetId} not found")
            : GetPetByIdResult.Ok(pet));
    }
}