namespace Showcase.BlazorApp.Services;

/// <summary>
/// Gateway service - Accounts operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// List all accounts with optional limit.
    /// </summary>
    public async Task<Account[]?> ListAccountsAsync(
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new ListAccountsParameters(Limit: limit);
        var result = await listAccountsEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.ToArray()
            : null;
    }

    /// <summary>
    /// Create a new account.
    /// </summary>
    public async Task<Account?> CreateAccountAsync(
        Account account,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateAccountParameters(Request: account);
        var result = await createAccountEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsCreated
            ? result.CreatedContent
            : null;
    }

    /// <summary>
    /// Get an account by ID.
    /// </summary>
    public async Task<Account?> GetAccountByIdAsync(
        string accountId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new GetAccountByIdParameters(AccountId: accountId);
        var result = await getAccountByIdEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : null;
    }

    /// <summary>
    /// Delete an account by ID.
    /// </summary>
    public async Task DeleteAccountByIdAsync(
        string accountId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DeleteAccountByIdParameters(AccountId: accountId);
        var result = await deleteAccountByIdEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsNoContent)
        {
            throw new HttpRequestException($"Failed to delete account: {result.StatusCode}");
        }
    }

    /// <summary>
    /// Update an account by ID.
    /// </summary>
    public async Task<Account?> UpdateAccountByIdAsync(
        string accountId,
        Account account,
        CancellationToken cancellationToken = default)
    {
        var parameters = new UpdateAccountByIdParameters(Request: account, AccountId: accountId);
        var result = await updateAccountByIdEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : null;
    }

    /// <summary>
    /// List paginated accounts.
    /// </summary>
    public async Task<PaginatedResult<Account>?> ListPaginatedAccountsAsync(
        int? pageSize = null,
        int? pageIndex = null,
        string? queryString = null,
        string? continuation = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new ListPaginatedAccountsParameters(
            QueryString: queryString,
            Continuation: continuation,
            PageSize: pageSize,
            PageIndex: pageIndex);

        var result = await listPaginatedAccountsEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result is not { IsOk: true, OkContent: JsonElement jsonElement })
        {
            return null;
        }

        return JsonSerializer.Deserialize<PaginatedResult<Account>>(
            jsonElement.GetRawText(),
            jsonOptions);
    }

    /// <summary>
    /// List accounts as async enumerable stream.
    /// Note: IAsyncEnumerable streaming requires special handling that may not be fully supported
    /// by the endpoint pattern. This implementation extracts items from the result.
    /// </summary>
    public async IAsyncEnumerable<Account> ListAsyncEnumerableAccountsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var result = await listAsyncEnumerableAccountsEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsOk)
        {
            yield break;
        }

        foreach (var item in result.OkContent)
        {
            yield return item;
        }
    }
}