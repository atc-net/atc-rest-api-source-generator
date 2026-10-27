namespace JsonLines.ApiHandlers;

/// <summary>
/// Handler business logic for the UpdateReadings operation.
/// </summary>
public sealed class UpdateReadingsHandler : IUpdateReadingsHandler
{
    /// <summary>
    /// Executes the UpdateReadings operation.
    /// </summary>
    public Task<UpdateReadingsResult> ExecuteAsync(
        UpdateReadingsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement updateReadings logic
        throw new NotImplementedException("updateReadings not implemented");
    }
}