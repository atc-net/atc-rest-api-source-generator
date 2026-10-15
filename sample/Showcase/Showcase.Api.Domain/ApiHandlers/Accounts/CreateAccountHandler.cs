namespace Showcase.Api.Domain.ApiHandlers.Accounts;

/// <summary>
/// Handler business logic for the CreateAccount operation.
/// </summary>
public sealed class CreateAccountHandler : ICreateAccountHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Accounts.CreateAccount");
    private readonly AccountInMemoryRepository repository;
    private readonly IOutputCacheStore outputCacheStore;

    public CreateAccountHandler(
        AccountInMemoryRepository repository,
        IOutputCacheStore outputCacheStore)
    {
        this.repository = repository;
        this.outputCacheStore = outputCacheStore;
    }

    public async Task<CreateAccountResult> ExecuteAsync(
        CreateAccountParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("CreateAccount");
        ArgumentNullException.ThrowIfNull(parameters);

        var account = await repository.Create(
            parameters.Request.Id,
            parameters.Request.Name,
            parameters.Request.Tag);

        // The account lists are cached under the tag the specification declares (x-cache-tags): evict them, so the
        // next read has the new account
        await outputCacheStore.EvictByTagAsync(OutputCacheTags.Accounts, cancellationToken);

        return CreateAccountResult.Created(account);
    }
}