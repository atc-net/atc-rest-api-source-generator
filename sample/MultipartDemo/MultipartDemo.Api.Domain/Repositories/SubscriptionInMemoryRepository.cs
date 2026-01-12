namespace MultipartDemo.Api.Domain.Repositories;

public sealed class SubscriptionInMemoryRepository
{
    private readonly ConcurrentDictionary<Guid, Subscription> subscriptions = new();

    public Task<IReadOnlyList<Subscription>> GetAllAsync()
        => Task.FromResult<IReadOnlyList<Subscription>>(subscriptions.Values.ToList());

    public Task<Subscription?> GetByIdAsync(Guid id)
    {
        subscriptions.TryGetValue(id, out var subscription);
        return Task.FromResult(subscription);
    }

    public Task<Subscription> CreateAsync(
        string name,
        NotificationType[] topics,
        Uri? callbackUrl,
        int? expiresInMinutes)
    {
        var subscription = new Subscription(
            Id: Guid.NewGuid(),
            Name: name,
            Topics: topics.ToList(),
            CallbackUrl: callbackUrl,
            ConnectionId: null,
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: expiresInMinutes.HasValue
                ? DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes.Value)
                : null,
            LastNotificationAt: null,
            IsActive: true);

        subscriptions[subscription.Id] = subscription;
        return Task.FromResult(subscription);
    }

    public Task<bool> DeleteAsync(Guid id)
        => Task.FromResult(subscriptions.TryRemove(id, out _));
}