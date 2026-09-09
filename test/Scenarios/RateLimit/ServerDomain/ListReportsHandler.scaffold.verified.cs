namespace RateLimit.ApiHandlers;

/// <summary>
/// Handler business logic for the ListReports operation.
/// </summary>
public sealed class ListReportsHandler : IListReportsHandler
{
    public Task<ListReportsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listReports logic
        throw new NotImplementedException("listReports not implemented");
    }
}