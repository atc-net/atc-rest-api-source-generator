namespace PetStoreFull.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the DeletePet operation.
/// </summary>
public sealed class DeletePetHandler : IDeletePetHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public DeletePetHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<DeletePetResult> ExecuteAsync(
        DeletePetParameters parameters,
        CancellationToken cancellationToken = default)
        => Task.FromResult(repository.RemovePet(parameters.PetId)
            ? DeletePetResult.Ok()
            : DeletePetResult.Error($"Pet {parameters.PetId} not found", StatusCodes.Status404NotFound));
}