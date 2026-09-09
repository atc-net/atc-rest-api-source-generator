namespace RateLimit.ApiHandlers;

/// <summary>
/// Handler business logic for the CreateExport operation.
/// </summary>
public sealed class CreateExportHandler : ICreateExportHandler
{
    public Task<CreateExportResult> ExecuteAsync(
        CreateExportParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createExport logic
        throw new NotImplementedException("createExport not implemented");
    }
}