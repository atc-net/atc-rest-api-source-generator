namespace VersioningHeader.ApiHandlers;

/// <summary>
/// Handler business logic for the GetOwnerById operation.
/// </summary>
public sealed class GetOwnerByIdHandler : IGetOwnerByIdHandler
{
    public Task<GetOwnerByIdResult> ExecuteAsync(
        GetOwnerByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getOwnerById logic
        throw new NotImplementedException("getOwnerById not implemented");
    }
}