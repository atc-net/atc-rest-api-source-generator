namespace PetStoreFull.Api.Domain.ApiHandlers.Pets;

/// <summary>
/// Handler business logic for the UpdatePetWithForm operation.
/// </summary>
public sealed class UpdatePetWithFormHandler : IUpdatePetWithFormHandler
{
    public Task<UpdatePetWithFormResult> ExecuteAsync(
        UpdatePetWithFormParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement updatePetWithForm logic
        throw new NotImplementedException("updatePetWithForm not implemented");
    }
}