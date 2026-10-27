namespace PetStoreSimple.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the ShowPetById operation.
/// </summary>
public sealed class ShowPetByIdHandler : IShowPetByIdHandler
{
    private readonly PetInMemoryRepository repository;

    public ShowPetByIdHandler(PetInMemoryRepository repository)
        => this.repository = repository;

    public Task<ShowPetByIdResult> ExecuteAsync(
        ShowPetByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var pet = repository.GetById(parameters.PetId);

        // The specification declares only the default (Error) response for a missing pet
        return Task.FromResult(pet is null
            ? ShowPetByIdResult.Error(
                new Error(StatusCodes.Status404NotFound, $"Pet '{parameters.PetId}' not found"),
                StatusCodes.Status404NotFound)
            : ShowPetByIdResult.Ok(pet));
    }
}