namespace MultipartDemo.Api.Services;

/// <summary>
/// Service for managing notification subscriptions and sending notifications via SignalR.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> hubContext;
    private readonly ILogger<NotificationService> logger;
    private readonly ConcurrentDictionary<string, SubscriptionInfo> subscriptions = new(StringComparer.CurrentCulture);
    private readonly ConcurrentDictionary<string, HashSet<string>> connectionSubscriptions = new(StringComparer.CurrentCulture);

    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        this.hubContext = hubContext;
        this.logger = logger;
    }

    public Task<string> SubscribeAsync(
        string connectionId,
        string[] topics)
    {
        var subscriptionId = Guid.NewGuid().ToString();
        var subscription = new SubscriptionInfo(
            subscriptionId,
            connectionId,
            topics,
            DateTime.UtcNow,
            null);

        subscriptions[subscriptionId] = subscription;

        // Track subscriptions by connection
        connectionSubscriptions.AddOrUpdate(
            connectionId,
            _ => [subscriptionId],
            (_, existing) =>
            {
                existing.Add(subscriptionId);
                return existing;
            });

        logger.LogInformation(
            "Created subscription {SubscriptionId} for connection {ConnectionId} with topics: {Topics}",
            subscriptionId,
            connectionId,
            string.Join(", ", topics));
        return Task.FromResult(subscriptionId);
    }

    public Task<string[]> UnsubscribeAsync(string subscriptionId)
    {
        if (!subscriptions.TryRemove(subscriptionId, out var subscription))
        {
            return Task.FromResult(Array.Empty<string>());
        }

        // Remove from connection tracking
        if (connectionSubscriptions.TryGetValue(subscription.ConnectionId, out var subs))
        {
            subs.Remove(subscriptionId);
        }

        logger.LogInformation(
            "Removed subscription {SubscriptionId}",
            subscriptionId);

        return Task.FromResult(subscription.Topics);
    }

    public Task UnsubscribeAllAsync(string connectionId)
    {
        if (!connectionSubscriptions.TryRemove(connectionId, out var subscriptionIds))
        {
            return Task.CompletedTask;
        }

        foreach (var subscriptionId in subscriptionIds)
        {
            subscriptions.TryRemove(subscriptionId, out _);
        }

        logger.LogInformation(
            "Removed all subscriptions for connection {ConnectionId}",
            connectionId);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptionsAsync(
        string connectionId)
    {
        if (!connectionSubscriptions.TryGetValue(connectionId, out var subscriptionIds))
        {
            return Task.FromResult<IReadOnlyList<SubscriptionInfo>>([]);
        }

        var result = subscriptionIds
            .Select(id => subscriptions.GetValueOrDefault(id))
            .Where(s => s != null)
            .Cast<SubscriptionInfo>()
            .ToList();

        return Task.FromResult<IReadOnlyList<SubscriptionInfo>>(result);
    }

    public Task<IReadOnlyList<SubscriptionInfo>> GetAllSubscriptionsAsync()
    {
        var result = subscriptions.Values.ToList();
        return Task.FromResult<IReadOnlyList<SubscriptionInfo>>(result);
    }

    public async Task SendSystemNotificationAsync(
        SystemNotification notification)
    {
        logger.LogDebug(
            "Sending system notification: {Type} - {Message}",
            notification.Type,
            notification.Message);

        // Send to all clients in the "System" group
        await hubContext.Clients
            .Group("System")
            .SendAsync("SystemNotification", notification);

        // Also send to "Alert" and "Metric" groups if applicable
        if (notification.Severity is NotificationSeverity.Warning or NotificationSeverity.Error or NotificationSeverity.Critical)
        {
            await hubContext.Clients
                .Group("Alert")
                .SendAsync("SystemNotification", notification);
        }

        if (notification.Metrics != null)
        {
            await hubContext.Clients
                .Group("Metric")
                .SendAsync("SystemNotification", notification);
        }

        UpdateLastNotificationTime("System");
    }

    public async Task SendUserActivityEventAsync(
        UserActivityEvent activityEvent)
    {
        logger.LogDebug(
            "Sending user activity event: {Action} for user {UserId}",
            activityEvent.Action,
            activityEvent.UserId);

        await hubContext.Clients
            .Group("User")
            .SendAsync("UserActivity", activityEvent);

        UpdateLastNotificationTime("User");
    }

    public async Task SendDataChangeEventAsync(DataChangeEvent changeEvent)
    {
        logger.LogDebug(
            "Sending data change event: {Operation} on {EntityType}/{EntityId}",
            changeEvent.Operation,
            changeEvent.EntityType,
            changeEvent.EntityId);

        await hubContext.Clients
            .Group("Data")
            .SendAsync("DataChange", changeEvent);

        UpdateLastNotificationTime("Data");
    }

    private void UpdateLastNotificationTime(string topic)
    {
        var now = DateTime.UtcNow;
        foreach (var (id, subscription) in subscriptions)
        {
            if (subscription.Topics.Contains(topic, StringComparer.OrdinalIgnoreCase))
            {
                subscriptions[id] = subscription with { LastNotificationAt = now };
            }
        }
    }
}