namespace Showcase.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the GetAccountById operation.
/// </summary>
public sealed class GetAccountByIdHandler : IGetAccountByIdHandler
{
    private readonly AccountInMemoryRepository repository;

    public GetAccountByIdHandler(AccountInMemoryRepository repository)
        => this.repository = repository;

    public async Task<GetAccountByIdResult> ExecuteAsync(
        GetAccountByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var account = await repository.GetById(parameters.AccountId);

        return account is null
            ? GetAccountByIdResult.NotFound()
            : GetAccountByIdResult.Ok(account);
    }
}