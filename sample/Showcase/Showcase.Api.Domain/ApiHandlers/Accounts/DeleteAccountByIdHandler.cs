namespace Showcase.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the DeleteAccountById operation.
/// </summary>
public sealed class DeleteAccountByIdHandler : IDeleteAccountByIdHandler
{
    private readonly AccountInMemoryRepository repository;

    public DeleteAccountByIdHandler(AccountInMemoryRepository repository)
        => this.repository = repository;

    public async Task<DeleteAccountByIdResult> ExecuteAsync(
        DeleteAccountByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var account = await repository.Delete(parameters.AccountId);

        return account is null
            ? DeleteAccountByIdResult.NotFound()
            : DeleteAccountByIdResult.NoContent();
    }
}