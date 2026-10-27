namespace Deprecations.ApiHandlers;

/// <summary>
/// Handler business logic for the ListLegacyPets operation.
/// </summary>
public sealed class ListLegacyPetsHandler : IListLegacyPetsHandler
{
    /// <summary>
    /// Executes the ListLegacyPets operation.
    /// </summary>
    public Task<ListLegacyPetsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listLegacyPets logic
        throw new NotImplementedException("listLegacyPets not implemented");
    }
}