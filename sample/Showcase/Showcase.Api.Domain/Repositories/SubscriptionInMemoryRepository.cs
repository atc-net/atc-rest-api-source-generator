namespace Showcase.Api.Domain.Repositories;

/// <summary>
/// In-memory repository for managing notification subscriptions.
/// </summary>
public sealed class SubscriptionInMemoryRepository
{
    private readonly ConcurrentDictionary<Guid, Subscription> subscriptions = new();

    /// <summary>
    /// Gets all subscriptions.
    /// </summary>
    public List<Subscription> GetAll()
        => subscriptions.Values.ToList();

    /// <summary>
    /// Gets a subscription by ID.
    /// </summary>
    public Subscription? GetById(Guid id)
        => subscriptions.GetValueOrDefault(id);

    /// <summary>
    /// Creates a new subscription.
    /// </summary>
    public Subscription Create(CreateSubscriptionRequest request)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = request.ExpiresInMinutes.HasValue
            ? now.AddMinutes(request.ExpiresInMinutes.Value)
            : (DateTimeOffset?)null;

        var subscription = new Subscription(
            Id: id,
            Name: request.Name,
            Topics: request.Topics,
            CallbackUrl: request.CallbackUrl,
            ConnectionId: null,
            CreatedAt: now,
            ExpiresAt: expiresAt,
            LastNotificationAt: null,
            IsActive: true);

        subscriptions[id] = subscription;
        return subscription;
    }

    /// <summary>
    /// Updates the connection ID for a subscription.
    /// </summary>
    public bool UpdateConnectionId(
        Guid id,
        string? connectionId)
    {
        if (!subscriptions.TryGetValue(id, out var subscription))
        {
            return false;
        }

        subscriptions[id] = subscription with { ConnectionId = connectionId };
        return true;
    }

    /// <summary>
    /// Updates the last notification timestamp.
    /// </summary>
    public void UpdateLastNotificationAt(Guid id)
    {
        if (subscriptions.TryGetValue(id, out var subscription))
        {
            subscriptions[id] = subscription with { LastNotificationAt = DateTimeOffset.UtcNow };
        }
    }

    /// <summary>
    /// Deletes a subscription.
    /// </summary>
    public bool Delete(Guid id)
        => subscriptions.TryRemove(id, out _);

    /// <summary>
    /// Gets all active subscriptions for a specific topic.
    /// </summary>
    public Subscription[] GetByTopic(NotificationType topic)
        => subscriptions.Values
            .Where(s => s.IsActive && s.Topics.Contains(topic))
            .Where(s => !s.ExpiresAt.HasValue || s.ExpiresAt > DateTimeOffset.UtcNow)
            .ToArray();
}