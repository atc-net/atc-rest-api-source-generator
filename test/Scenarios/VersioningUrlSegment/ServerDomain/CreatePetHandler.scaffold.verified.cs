namespace VersioningUrlSegment.ApiHandlers;

/// <summary>
/// Handler business logic for the CreatePet operation.
/// </summary>
public sealed class CreatePetHandler : ICreatePetHandler
{
    public Task<CreatePetResult> ExecuteAsync(
        CreatePetParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createPet logic
        throw new NotImplementedException("createPet not implemented");
    }
}