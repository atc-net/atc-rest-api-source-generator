namespace Showcase.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the ListPaginatedAccounts operation.
/// </summary>
public sealed class ListPaginatedAccountsHandler : IListPaginatedAccountsHandler
{
    private readonly AccountInMemoryRepository repository;

    public ListPaginatedAccountsHandler(AccountInMemoryRepository repository)
        => this.repository = repository;

    public async Task<ListPaginatedAccountsResult> ExecuteAsync(
        ListPaginatedAccountsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var pageSize = parameters.PageSize ?? 10;

        // Parse continuation token if provided, otherwise use pageIndex parameter
        var pageIndex = ParseContinuationToken(parameters.Continuation) ?? parameters.PageIndex ?? 0;

        var (items, totalCount) = await repository.GetPaginated(
            pageSize,
            pageIndex,
            parameters.QueryString);

        // Generate continuation token if there are more pages
        var hasMorePages = (pageIndex + 1) * pageSize < totalCount;
        var continuationToken = hasMorePages
            ? $"page:{pageIndex + 1}"
            : null;

        var result = new PaginatedResult<Account>(
            PageSize: pageSize,
            PageIndex: pageIndex,
            QueryString: parameters.QueryString,
            Continuation: continuationToken,
            Count: items.Length,
            TotalCount: totalCount,
            Results: items);

        return ListPaginatedAccountsResult.Ok(result);
    }

    private static int? ParseContinuationToken(string? continuation)
    {
        if (string.IsNullOrEmpty(continuation))
        {
            return null;
        }

        // Expected format: "page:{pageIndex}"
        const string prefix = "page:";
        if (continuation.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(continuation.AsSpan(prefix.Length), out var pageIndex))
        {
            return pageIndex;
        }

        return null;
    }
}