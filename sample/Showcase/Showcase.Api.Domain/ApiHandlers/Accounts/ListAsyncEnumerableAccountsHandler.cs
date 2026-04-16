namespace Showcase.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the ListAsyncEnumerableAccounts operation.
/// Demonstrates streaming responses using IAsyncEnumerable.
/// </summary>
public sealed class ListAsyncEnumerableAccountsHandler : IListAsyncEnumerableAccountsHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Accounts.ListAsyncEnumerableAccounts");
    private readonly AccountInMemoryRepository repository;

    public ListAsyncEnumerableAccountsHandler(
        AccountInMemoryRepository repository)
        => this.repository = repository;

    public Task<ListAsyncEnumerableAccountsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ListAsyncEnumerableAccounts");

        // Get streaming accounts from repository
        var streamingAccounts = repository.GetAllStreaming(cancellationToken: cancellationToken);

        // Return the IAsyncEnumerable wrapped in the result
        return Task.FromResult(ListAsyncEnumerableAccountsResult.Ok(streamingAccounts));
    }
}