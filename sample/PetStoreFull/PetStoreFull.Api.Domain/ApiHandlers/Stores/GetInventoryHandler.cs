namespace PetStoreFull.Api.Domain.ApiHandlers.Stores;

/// <summary>
/// Handler business logic for the GetInventory operation.
/// </summary>
public sealed class GetInventoryHandler : IGetInventoryHandler
{
    private readonly PetStoreInMemoryRepository repository;

    public GetInventoryHandler(PetStoreInMemoryRepository repository)
        => this.repository = repository;

    public Task<GetInventoryResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(GetInventoryResult.Ok(repository.GetInventory()));
}