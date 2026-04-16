namespace Showcase.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the ListAccounts operation.
/// </summary>
public sealed class ListAccountsHandler : IListAccountsHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Accounts.ListAccounts");
    private readonly AccountInMemoryRepository repository;

    public ListAccountsHandler(AccountInMemoryRepository repository)
        => this.repository = repository;

    public async Task<ListAccountsResult> ExecuteAsync(
        ListAccountsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ListAccounts");
        ArgumentNullException.ThrowIfNull(parameters);

        var accounts = await repository.GetAll(parameters.Limit);

        return await Task.FromResult(ListAccountsResult.Ok(accounts));
    }
}