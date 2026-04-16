namespace Showcase.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the DeleteAccountById operation.
/// </summary>
public sealed class DeleteAccountByIdHandler : IDeleteAccountByIdHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Accounts.DeleteAccountById");
    private readonly AccountInMemoryRepository repository;

    public DeleteAccountByIdHandler(AccountInMemoryRepository repository)
        => this.repository = repository;

    public async Task<DeleteAccountByIdResult> ExecuteAsync(
        DeleteAccountByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("DeleteAccountById");
        ArgumentNullException.ThrowIfNull(parameters);

        var account = await repository.Delete(parameters.AccountId);

        return account is null
            ? DeleteAccountByIdResult.NotFound()
            : DeleteAccountByIdResult.NoContent();
    }
}