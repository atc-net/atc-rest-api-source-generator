namespace Showcase.BlazorApp.Pages;

public sealed partial class WebhookDemo : IAsyncDisposable
{
    [Inject]
    private NotificationHubService HubService { get; set; } = null!;

    [Inject]
    private HttpClient Http { get; set; } = null!;

    [Inject]
    private IConfiguration Configuration { get; set; } = null!;

    private readonly List<SystemNotification> notifications = [];
    private bool isConnecting;
    private bool isSubscribed;
    private bool isSending;
    private string? lastError;

    protected override void OnInitialized()
    {
        HubService.OnSystemNotification += HandleSystemNotification;
        HubService.OnUserActivity += HandleUserActivity;
        HubService.OnDataChange += HandleDataChange;
        HubService.OnConnectionStateChanged += HandleConnectionStateChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Auto-connect on page load
            await ConnectAsync();
            if (HubService.IsConnected)
            {
                await SubscribeAsync();
            }
        }
    }

    private async Task ConnectAsync()
    {
        isConnecting = true;
        lastError = null;
        try
        {
            await HubService.ConnectAsync();
        }
        catch (Exception ex)
        {
            lastError = $"Connection failed: {ex.Message}";
        }
        finally
        {
            isConnecting = false;
        }
    }

    private async Task SubscribeAsync()
    {
        var subscriptionId = await HubService.SubscribeAsync(
            ["System", "User", "Data", "Alert", "Metric"]);
        isSubscribed = subscriptionId != null;
    }

    private async Task TriggerSystemNotification()
    {
        await SendWebhookAsync("/webhooks/system-notification", new
        {
            id = Guid.NewGuid(),
            type = "Alert",
            severity = "Warning",
            message = $"System alert triggered from Blazor UI at {DateTime.UtcNow:HH:mm:ss}",
            timestamp = DateTimeOffset.UtcNow,
            data = "Triggered via WebhookDemo page",
        });
    }

    private async Task TriggerUserActivity()
    {
        var actions = new[] { "Login", "Logout", "ProfileUpdate", "PasswordChange", "AccountCreated", "AccountDeleted" };

        await SendWebhookAsync("/webhooks/user-activity", new
        {
            id = Guid.NewGuid(),
            userId = Guid.NewGuid(),
            action = actions[GetRandomIndex(actions.Length)],
            timestamp = DateTimeOffset.UtcNow,
            ipAddress = $"192.168.1.{GetRandomIndex(255) + 1}",
            userAgent = "BlazorApp/1.0",
            details = "Activity triggered via WebhookDemo page",
        });
    }

    private async Task TriggerDataChange()
    {
        var entityTypes = new[] { "Account", "User", "Task", "File" };
        var operations = new[] { "Created", "Updated", "Deleted" };

        await SendWebhookAsync("/webhooks/data-change", new
        {
            id = Guid.NewGuid(),
            entityType = entityTypes[GetRandomIndex(entityTypes.Length)],
            entityId = Guid.NewGuid().ToString(),
            operation = operations[GetRandomIndex(operations.Length)],
            timestamp = DateTimeOffset.UtcNow,
            performedBy = (Guid?)null,
            previousValue = "old-value",
            newValue = "new-value",
        });
    }

#pragma warning disable CA5394 // Random is insecure - acceptable for demo
    private static int GetRandomIndex(int maxValue)
        => Random.Shared.Next(maxValue);
#pragma warning restore CA5394

    private async Task SendWebhookAsync(
        string endpoint,
        object payload)
    {
        isSending = true;
        lastError = null;
        StateHasChanged();

        try
        {
#pragma warning disable S1075 // Hardcoded localhost is acceptable as fallback for local development
            var baseUrl = Configuration["ApiBaseUrl"] ?? "https://localhost:5001";
#pragma warning restore S1075
            var response = await Http.PostAsJsonAsync($"{baseUrl}{endpoint}", payload);

            if (!response.IsSuccessStatusCode)
            {
                lastError = $"Webhook failed: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            lastError = $"Error sending webhook: {ex.Message}";
        }
        finally
        {
            isSending = false;
            StateHasChanged();
        }
    }

    private void ClearNotifications()
    {
        notifications.Clear();
        lastError = null;
    }

    private void HandleSystemNotification(SystemNotification notification)
    {
        notifications.Add(notification);
        _ = InvokeAsync(StateHasChanged);
    }

    private void HandleUserActivity(UserActivityEvent activity)
    {
        // Convert user activity to a system notification for display
        var notification = new SystemNotification(
            Id: Guid.NewGuid(),
            Type: NotificationType.User,
            Severity: NotificationSeverity.Info,
            Timestamp: activity.Timestamp,
            Message: $"User {activity.UserId} performed {activity.Action}",
            Data: activity.Details,
            Metrics: null!);
        notifications.Add(notification);
        _ = InvokeAsync(StateHasChanged);
    }

    private void HandleDataChange(DataChangeEvent change)
    {
        // Convert data change to a system notification for display
        var notification = new SystemNotification(
            Id: Guid.NewGuid(),
            Type: NotificationType.Data,
            Severity: NotificationSeverity.Info,
            Timestamp: change.Timestamp,
            Message: $"{change.Operation} {change.EntityType}/{change.EntityId}",
            Data: $"Previous: {change.PreviousValue}, New: {change.NewValue}",
            Metrics: null!);
        notifications.Add(notification);
        _ = InvokeAsync(StateHasChanged);
    }

    private void HandleConnectionStateChanged(string state)
    {
        if (state == "Disconnected")
        {
            isSubscribed = false;
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private Color GetConnectionColor()
        => HubService.ConnectionState switch
        {
            "Connected" => Color.Success,
            "Connecting" or "Reconnecting" => Color.Warning,
            _ => Color.Error,
        };

    private static string GetNotificationIcon(NotificationType type)
        => type switch
        {
            NotificationType.System => Icons.Material.Filled.Computer,
            NotificationType.User => Icons.Material.Filled.Person,
            NotificationType.Data => Icons.Material.Filled.Storage,
            NotificationType.Alert => Icons.Material.Filled.Warning,
            NotificationType.Metric => Icons.Material.Filled.Analytics,
            _ => Icons.Material.Filled.Notifications,
        };

    private static Color GetSeverityColor(NotificationSeverity severity)
        => severity switch
        {
            NotificationSeverity.Info => Color.Info,
            NotificationSeverity.Warning => Color.Warning,
            NotificationSeverity.Error => Color.Error,
            NotificationSeverity.Critical => Color.Error,
            _ => Color.Default,
        };

    private static Color GetTypeChipColor(NotificationType type)
        => type switch
        {
            NotificationType.System => Color.Primary,
            NotificationType.User => Color.Secondary,
            NotificationType.Data => Color.Tertiary,
            NotificationType.Alert => Color.Warning,
            NotificationType.Metric => Color.Info,
            _ => Color.Default,
        };

    private static Color GetSeverityChipColor(NotificationSeverity severity)
        => severity switch
        {
            NotificationSeverity.Info => Color.Info,
            NotificationSeverity.Warning => Color.Warning,
            NotificationSeverity.Error => Color.Error,
            NotificationSeverity.Critical => Color.Dark,
            _ => Color.Default,
        };

    public async ValueTask DisposeAsync()
    {
        HubService.OnSystemNotification -= HandleSystemNotification;
        HubService.OnUserActivity -= HandleUserActivity;
        HubService.OnDataChange -= HandleDataChange;
        HubService.OnConnectionStateChanged -= HandleConnectionStateChanged;

        // Don't dispose the hub service - it's a singleton shared across pages
        await Task.CompletedTask;
    }
}