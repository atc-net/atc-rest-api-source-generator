namespace Showcase.Api.Domain.ApiHandlers.Notifications;

/// <summary>
/// Handler business logic for the ListSubscriptions operation.
/// </summary>
public sealed class ListSubscriptionsHandler : IListSubscriptionsHandler
{
    private readonly SubscriptionInMemoryRepository repository;

    public ListSubscriptionsHandler(SubscriptionInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<ListSubscriptionsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var subscriptions = repository.GetAll();
        return Task.FromResult(ListSubscriptionsResult.Ok(subscriptions));
    }
}