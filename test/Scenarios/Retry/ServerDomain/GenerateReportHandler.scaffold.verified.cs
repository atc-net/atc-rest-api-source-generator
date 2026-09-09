namespace Retry.ApiHandlers;

/// <summary>
/// Handler business logic for the GenerateReport operation.
/// </summary>
public sealed class GenerateReportHandler : IGenerateReportHandler
{
    public Task<GenerateReportResult> ExecuteAsync(
        GenerateReportParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement generateReport logic
        throw new NotImplementedException("generateReport not implemented");
    }
}