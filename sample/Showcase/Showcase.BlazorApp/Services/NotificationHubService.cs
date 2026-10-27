namespace Showcase.BlazorApp.Services;

/// <summary>
/// Service for connecting to the SignalR notification hub, through the hub client generated from Showcase.yaml
/// (<c>x-signalr-hubs</c>): its events and methods by name and type.
/// </summary>
#pragma warning disable CA1003 // Use generic event handler instances
#pragma warning disable MA0046 // The delegate must have 2 parameters
public sealed class NotificationHubService : IAsyncDisposable
{
    private readonly IConfiguration configuration;
    private NotificationsHubClient? hub;
    private string? currentSubscriptionId;

    public event Action<SystemNotification>? OnSystemNotification;

    public event Action<UserActivityEvent>? OnUserActivity;

    public event Action<DataChangeEvent>? OnDataChange;

    public event Action<string>? OnConnectionStateChanged;

    public bool IsConnected
        => hub?.Connection.State == HubConnectionState.Connected;

    public string ConnectionState
        => hub?.Connection.State.ToString() ?? "Disconnected";

    public NotificationHubService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public async Task ConnectAsync()
    {
        if (hub is not null)
        {
            return;
        }

        var baseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:5001";

        hub = new NotificationsHubClient(
            new HubConnectionBuilder()
                .WithUrl(baseUrl.TrimEnd('/') + NotificationsHubClient.Url)
                .WithAutomaticReconnect()
                .Build());

        // The hub's events, by the names and payload types the specification declares
        hub.OnSystemNotification(notification => OnSystemNotification?.Invoke(notification));
        hub.OnUserActivity(activity => OnUserActivity?.Invoke(activity));
        hub.OnDataChange(change => OnDataChange?.Invoke(change));

        var hubConnection = hub.Connection;
        hubConnection.Reconnecting += error =>
        {
            OnConnectionStateChanged?.Invoke("Reconnecting...");
            return Task.CompletedTask;
        };

        hubConnection.Reconnected += connectionId =>
        {
            OnConnectionStateChanged?.Invoke("Connected");
            return Task.CompletedTask;
        };

        hubConnection.Closed += error =>
        {
            OnConnectionStateChanged?.Invoke("Disconnected");
            return Task.CompletedTask;
        };

        await hub.StartAsync();
        OnConnectionStateChanged?.Invoke("Connected");
    }

    public async Task<string?> SubscribeAsync(string[] topics)
    {
        if (hub is null || hub.Connection.State != HubConnectionState.Connected)
        {
            return null;
        }

        currentSubscriptionId = await hub.SubscribeAsync(topics);
        return currentSubscriptionId;
    }

    public async Task UnsubscribeAsync()
    {
        if (hub is null || currentSubscriptionId is null)
        {
            return;
        }

        await hub.UnsubscribeAsync(currentSubscriptionId);
        currentSubscriptionId = null;
    }

    public async Task DisconnectAsync()
    {
        if (hub is not null)
        {
            await hub.StopAsync();
            await hub.DisposeAsync();
            hub = null;
            currentSubscriptionId = null;
            OnConnectionStateChanged?.Invoke("Disconnected");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
#pragma warning restore MA0046 // The delegate must have 2 parameters
#pragma warning restore CA1003 // Use generic event handler instances