namespace Showcase.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the DeleteAccountById operation.
/// </summary>
public sealed class DeleteAccountByIdHandler : IDeleteAccountByIdHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Accounts.DeleteAccountById");
    private readonly AccountInMemoryRepository repository;
    private readonly IOutputCacheStore outputCacheStore;

    public DeleteAccountByIdHandler(
        AccountInMemoryRepository repository,
        IOutputCacheStore outputCacheStore)
    {
        this.repository = repository;
        this.outputCacheStore = outputCacheStore;
    }

    public async Task<DeleteAccountByIdResult> ExecuteAsync(
        DeleteAccountByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("DeleteAccountById");
        ArgumentNullException.ThrowIfNull(parameters);

        var account = await repository.Delete(parameters.AccountId);

        if (account is null)
        {
            return DeleteAccountByIdResult.NotFound();
        }

        await outputCacheStore.EvictByTagAsync(OutputCacheTags.Accounts, cancellationToken);
        return DeleteAccountByIdResult.NoContent();
    }
}