namespace MultipartDemo.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the ListAccounts operation.
/// </summary>
public sealed class ListAccountsHandler : IListAccountsHandler
{
    public System.Threading.Tasks.Task<ListAccountsResult> ExecuteAsync(
        ListAccountsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listAccounts logic
        throw new NotImplementedException("listAccounts not implemented");
    }
}