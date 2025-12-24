namespace Showcase.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the UpdateAccountById operation.
/// </summary>
public sealed class UpdateAccountByIdHandler : IUpdateAccountByIdHandler
{
    private readonly AccountInMemoryRepository repository;

    public UpdateAccountByIdHandler(AccountInMemoryRepository repository)
        => this.repository = repository;

    public async Task<UpdateAccountByIdResult> ExecuteAsync(
        UpdateAccountByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var account = await repository.Update(
            parameters.AccountId,
            parameters.Request.Name,
            parameters.Request.Tag);

        return account is null
            ? UpdateAccountByIdResult.NotFound()
            : UpdateAccountByIdResult.Ok(account);
    }
}