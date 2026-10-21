namespace JsonLines.ApiHandlers;

/// <summary>
/// Handler business logic for the ImportReadings operation.
/// </summary>
public sealed class ImportReadingsHandler : IImportReadingsHandler
{
    /// <summary>
    /// Executes the ImportReadings operation.
    /// </summary>
    public Task<ImportReadingsResult> ExecuteAsync(
        ImportReadingsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement importReadings logic
        throw new NotImplementedException("importReadings not implemented");
    }
}