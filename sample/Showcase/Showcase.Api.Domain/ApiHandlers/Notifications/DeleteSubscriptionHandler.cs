namespace Showcase.Api.Domain.ApiHandlers.Notifications;

/// <summary>
/// Handler business logic for the DeleteSubscription operation.
/// </summary>
public sealed class DeleteSubscriptionHandler : IDeleteSubscriptionHandler
{
    private readonly SubscriptionInMemoryRepository repository;

    public DeleteSubscriptionHandler(SubscriptionInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<DeleteSubscriptionResult> ExecuteAsync(
        DeleteSubscriptionParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var deleted = repository.Delete(parameters.SubscriptionId);

        if (!deleted)
        {
            return Task.FromResult(DeleteSubscriptionResult.NotFound());
        }

        return Task.FromResult(DeleteSubscriptionResult.NoContent());
    }
}