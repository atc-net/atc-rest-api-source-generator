namespace JsonLines.ApiHandlers;

/// <summary>
/// Handler business logic for the NormalizeReadings operation.
/// </summary>
public sealed class NormalizeReadingsHandler : INormalizeReadingsHandler
{
    /// <summary>
    /// Executes the NormalizeReadings operation.
    /// </summary>
    public Task<NormalizeReadingsResult> ExecuteAsync(
        NormalizeReadingsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement normalizeReadings logic
        throw new NotImplementedException("normalizeReadings not implemented");
    }
}