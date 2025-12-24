namespace Showcase.BlazorApp.Services;

/// <summary>
/// Service for connecting to the SignalR notification hub.
/// </summary>
#pragma warning disable CA1003 // Use generic event handler instances
#pragma warning disable MA0046 // The delegate must have 2 parameters
public sealed class NotificationHubService : IAsyncDisposable
{
    private readonly IConfiguration configuration;
    private HubConnection? hubConnection;
    private string? currentSubscriptionId;

    public event Action<SystemNotification>? OnSystemNotification;

    public event Action<UserActivityEvent>? OnUserActivity;

    public event Action<DataChangeEvent>? OnDataChange;

    public event Action<string>? OnConnectionStateChanged;

    public bool IsConnected
        => hubConnection?.State == HubConnectionState.Connected;

    public string ConnectionState
        => hubConnection?.State.ToString() ?? "Disconnected";

    public NotificationHubService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public async Task ConnectAsync()
    {
        if (hubConnection != null)
        {
            return;
        }

        var baseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:5001";
        var hubUrl = $"{baseUrl}/hubs/notifications";

        hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        // Register event handlers
        hubConnection.On<SystemNotification>("SystemNotification", notification =>
        {
            OnSystemNotification?.Invoke(notification);
        });

        hubConnection.On<UserActivityEvent>("UserActivity", activity =>
        {
            OnUserActivity?.Invoke(activity);
        });

        hubConnection.On<DataChangeEvent>("DataChange", change =>
        {
            OnDataChange?.Invoke(change);
        });

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

        await hubConnection.StartAsync();
        OnConnectionStateChanged?.Invoke("Connected");
    }

    public async Task<string?> SubscribeAsync(string[] topics)
    {
        if (hubConnection == null || hubConnection.State != HubConnectionState.Connected)
        {
            return null;
        }

        currentSubscriptionId = await hubConnection.InvokeAsync<string>("Subscribe", topics);
        return currentSubscriptionId;
    }

    public async Task UnsubscribeAsync()
    {
        if (hubConnection == null || currentSubscriptionId == null)
        {
            return;
        }

        await hubConnection.InvokeAsync("Unsubscribe", currentSubscriptionId);
        currentSubscriptionId = null;
    }

    public async Task DisconnectAsync()
    {
        if (hubConnection != null)
        {
            await hubConnection.StopAsync();
            await hubConnection.DisposeAsync();
            hubConnection = null;
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