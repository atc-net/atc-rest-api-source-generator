namespace Showcase.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the UpdateAccountById operation.
/// </summary>
public sealed class UpdateAccountByIdHandler : IUpdateAccountByIdHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Accounts.UpdateAccountById");
    private readonly AccountInMemoryRepository repository;
    private readonly IOutputCacheStore outputCacheStore;

    public UpdateAccountByIdHandler(
        AccountInMemoryRepository repository,
        IOutputCacheStore outputCacheStore)
    {
        this.repository = repository;
        this.outputCacheStore = outputCacheStore;
    }

    public async Task<UpdateAccountByIdResult> ExecuteAsync(
        UpdateAccountByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("UpdateAccountById");
        ArgumentNullException.ThrowIfNull(parameters);

        var account = await repository.Update(
            parameters.AccountId,
            parameters.Request.Name,
            parameters.Request.Tag);

        if (account is null)
        {
            return UpdateAccountByIdResult.NotFound();
        }

        await outputCacheStore.EvictByTagAsync(OutputCacheTags.Accounts, cancellationToken);
        return UpdateAccountByIdResult.Ok(account);
    }
}