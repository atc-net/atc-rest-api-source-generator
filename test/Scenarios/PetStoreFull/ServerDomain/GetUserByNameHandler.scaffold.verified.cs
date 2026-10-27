namespace PetStoreFull.ApiHandlers;

/// <summary>
/// Handler business logic for the GetUserByName operation.
/// </summary>
public sealed class GetUserByNameHandler : IGetUserByNameHandler
{
    /// <summary>
    /// Executes the GetUserByName operation.
    /// </summary>
    public Task<GetUserByNameResult> ExecuteAsync(
        GetUserByNameParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement getUserByName logic
        throw new NotImplementedException("getUserByName not implemented");
    }
}