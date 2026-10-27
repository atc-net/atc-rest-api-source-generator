namespace JsonLines.ApiHandlers;

/// <summary>
/// Handler business logic for the ImportReadingSequence operation.
/// </summary>
public sealed class ImportReadingSequenceHandler : IImportReadingSequenceHandler
{
    /// <summary>
    /// Executes the ImportReadingSequence operation.
    /// </summary>
    public Task<ImportReadingSequenceResult> ExecuteAsync(
        ImportReadingSequenceParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement importReadingSequence logic
        throw new NotImplementedException("importReadingSequence not implemented");
    }
}