namespace PetStoreFull.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the UpdatePetWithForm operation.
/// </summary>
public sealed class UpdatePetWithFormHandler : IUpdatePetWithFormHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public UpdatePetWithFormHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<UpdatePetWithFormResult> ExecuteAsync(
        UpdatePetWithFormParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // The specification declares 400 as the only error for this operation
        var pet = repository.GetPet(parameters.PetId);
        if (pet is null)
        {
            return Task.FromResult(UpdatePetWithFormResult.BadRequest());
        }

        var status = pet.Status;
        if (parameters.Status is not null)
        {
            status = Enum.GetValues<PetStatus>()
                .Cast<PetStatus?>()
                .FirstOrDefault(candidate => string.Equals(candidate.ToWireValue(), parameters.Status, StringComparison.Ordinal));

            if (status is null)
            {
                return Task.FromResult(UpdatePetWithFormResult.BadRequest());
            }
        }

        var updated = repository.UpdatePet(pet with
        {
            Name = parameters.Name ?? pet.Name,
            Status = status,
        });

        return Task.FromResult(UpdatePetWithFormResult.Ok(updated!));
    }
}