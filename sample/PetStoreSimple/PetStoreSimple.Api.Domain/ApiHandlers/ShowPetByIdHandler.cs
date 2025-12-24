namespace PetStoreSimple.Api.Domain.ApiHandlers;

public class ShowPetByIdHandler : IShowPetByIdHandler
{
    private readonly PetInMemoryRepository repository;

    public ShowPetByIdHandler(PetInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<ShowPetByIdResult> ExecuteAsync(
        ShowPetByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var pet = repository.GetById(parameters.PetId);

        if (pet == null)
        {
            var error = new Error(404, $"Pet with id {parameters.PetId} not found");
            return Task.FromResult(ShowPetByIdResult.Error(error));
        }

        return Task.FromResult(ShowPetByIdResult.Ok(pet));
    }
}