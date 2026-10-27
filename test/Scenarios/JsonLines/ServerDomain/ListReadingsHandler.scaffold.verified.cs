namespace JsonLines.ApiHandlers;

/// <summary>
/// Handler business logic for the ListReadings operation.
/// </summary>
public sealed class ListReadingsHandler : IListReadingsHandler
{
    /// <summary>
    /// Executes the ListReadings operation.
    /// </summary>
    public Task<ListReadingsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listReadings logic
        throw new NotImplementedException("listReadings not implemented");
    }
}