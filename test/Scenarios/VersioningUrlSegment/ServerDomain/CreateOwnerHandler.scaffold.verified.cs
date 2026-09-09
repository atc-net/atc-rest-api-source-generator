namespace VersioningUrlSegment.ApiHandlers;

/// <summary>
/// Handler business logic for the CreateOwner operation.
/// </summary>
public sealed class CreateOwnerHandler : ICreateOwnerHandler
{
    public Task<CreateOwnerResult> ExecuteAsync(
        CreateOwnerParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createOwner logic
        throw new NotImplementedException("createOwner not implemented");
    }
}