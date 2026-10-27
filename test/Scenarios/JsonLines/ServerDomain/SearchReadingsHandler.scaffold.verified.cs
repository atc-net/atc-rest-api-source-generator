namespace JsonLines.ApiHandlers;

/// <summary>
/// Handler business logic for the SearchReadings operation.
/// </summary>
public sealed class SearchReadingsHandler : ISearchReadingsHandler
{
    /// <summary>
    /// Executes the SearchReadings operation.
    /// </summary>
    public Task<SearchReadingsResult> ExecuteAsync(
        SearchReadingsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement searchReadings logic
        throw new NotImplementedException("searchReadings not implemented");
    }
}