namespace SecurityHybrid.ApiHandlers;

/// <summary>
/// Handler business logic for the ListReports operation.
/// </summary>
public sealed class ListReportsHandler : IListReportsHandler
{
    /// <summary>
    /// Executes the ListReports operation.
    /// </summary>
    public Task<ListReportsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listReports logic
        throw new NotImplementedException("listReports not implemented");
    }
}