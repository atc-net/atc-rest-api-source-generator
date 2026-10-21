namespace JsonLines.ApiHandlers;

/// <summary>
/// Handler business logic for the ImportReadingsAnyFormat operation.
/// </summary>
public sealed class ImportReadingsAnyFormatHandler : IImportReadingsAnyFormatHandler
{
    /// <summary>
    /// Executes the ImportReadingsAnyFormat operation.
    /// </summary>
    public Task<ImportReadingsAnyFormatResult> ExecuteAsync(
        ImportReadingsAnyFormatParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement importReadingsAnyFormat logic
        throw new NotImplementedException("importReadingsAnyFormat not implemented");
    }
}