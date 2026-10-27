namespace Showcase.Api.Hubs;

/// <summary>
/// SignalR hub for real-time notifications.
/// Clients can subscribe to notification topics and receive events in real-time.
/// </summary>
/// <remarks>
/// The hub is declared in Showcase.yaml (<c>x-signalr-hubs</c>): its events are the generated
/// <see cref="INotificationsHubClient"/> (a typed hub sends them by name and payload), its methods the generated
/// <see cref="INotificationsHub"/>, so the hub cannot drift from what the clients are generated from.
/// </remarks>
public sealed class NotificationHub : Hub<INotificationsHubClient>, INotificationsHub
{
    private readonly INotificationService notificationService;
    private readonly ILogger<NotificationHub> logger;

    public NotificationHub(
        INotificationService notificationService,
        ILogger<NotificationHub> logger)
    {
        this.notificationService = notificationService;
        this.logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation(
            "Client connected: {ConnectionId}",
            Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation(
            "Client disconnected: {ConnectionId}, Reason: {Exception}",
            Context.ConnectionId,
            exception?.Message ?? "Normal disconnect");

        // Remove all subscriptions for this connection
        await notificationService.UnsubscribeAllAsync(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to notification topics.
    /// </summary>
    /// <param name="payload">List of topics to subscribe to (System, User, Data, Alert, Metric).</param>
    /// <returns>The subscription ID.</returns>
    public async Task<string> Subscribe(IReadOnlyList<string> payload)
    {
        string[] topics = [.. payload];
        logger.LogInformation(
            "Client {ConnectionId} subscribing to topics: {Topics}",
            Context.ConnectionId,
            string.Join(", ", topics));

        var subscriptionId = await notificationService.SubscribeAsync(
            Context.ConnectionId,
            topics);

        // Add connection to groups for each topic
        foreach (var topic in topics)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, topic);
        }

        return subscriptionId;
    }

    /// <summary>
    /// Unsubscribe from notifications.
    /// </summary>
    /// <param name="payload">The subscription ID to cancel.</param>
    public async Task Unsubscribe(string payload)
    {
        var subscriptionId = payload;
        logger.LogInformation(
            "Client {ConnectionId} unsubscribing: {SubscriptionId}",
            Context.ConnectionId,
            subscriptionId);

        var topics = await notificationService.UnsubscribeAsync(subscriptionId);

        // Remove connection from groups
        foreach (var topic in topics)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, topic);
        }
    }

    /// <summary>
    /// Get active subscriptions for the current connection.
    /// </summary>
    public Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptions()
        => notificationService.GetSubscriptionsAsync(Context.ConnectionId);
}