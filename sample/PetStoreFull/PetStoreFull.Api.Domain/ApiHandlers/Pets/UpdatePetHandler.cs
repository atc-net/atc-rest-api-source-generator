namespace PetStoreFull.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the UpdatePet operation.
/// </summary>
public sealed class UpdatePetHandler : IUpdatePetHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public UpdatePetHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<UpdatePetResult> ExecuteAsync(
        UpdatePetParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (parameters.Request.Id is null)
        {
            return Task.FromResult(UpdatePetResult.BadRequest());
        }

        var pet = repository.UpdatePet(parameters.Request);

        return Task.FromResult(pet is null
            ? UpdatePetResult.NotFound($"Pet {parameters.Request.Id} not found")
            : UpdatePetResult.Ok(pet));
    }
}