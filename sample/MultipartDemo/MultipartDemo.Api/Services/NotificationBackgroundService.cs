namespace MultipartDemo.Api.Services;

/// <summary>
/// Background service that generates periodic system notifications every 10 seconds.
/// Simulates real-time system metrics and alerts for demo purposes.
/// </summary>
public sealed class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<NotificationBackgroundService> logger;
    private readonly TimeSpan interval = TimeSpan.FromSeconds(10);
    private readonly Stopwatch uptime = Stopwatch.StartNew();
    private readonly Random random = new();
    private int notificationCount;

    public NotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationBackgroundService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Notification background service started");

        // Wait a bit before starting to allow the app to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendPeriodicNotificationAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error sending periodic notification");
            }

            await Task.Delay(interval, stoppingToken);
        }

        logger.LogInformation("Notification background service stopped");
    }

    private async Task SendPeriodicNotificationAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        notificationCount++;

        // Generate system metrics
        var metrics = GenerateSystemMetrics();

        // Determine severity based on metrics
        var severity = DetermineSeverity(metrics);

        // Create notification message based on severity
        var message = GenerateMessage(severity, metrics);

        var notification = new SystemNotification(
            Id: Guid.NewGuid(),
            Type: NotificationType.Metric,
            Severity: severity,
            Timestamp: DateTimeOffset.UtcNow,
            Message: message,
            Data: null,
            Metrics: metrics);

        await notificationService.SendSystemNotificationAsync(notification);

        logger.LogDebug(
            "Sent notification #{Count}: {Severity} - {Message}",
            notificationCount,
            severity,
            message);

        // Occasionally send additional alerts
        if (notificationCount % 5 == 0)
        {
            await SendRandomAlertAsync(notificationService);
        }
    }

    private SystemMetrics GenerateSystemMetrics()
    {
        // Simulate realistic system metrics with some variation
        var baseLoad = 30 + (Math.Sin(notificationCount * 0.1) * 20);

        return new SystemMetrics(
            CpuUsage: Math.Clamp(baseLoad + (random.NextDouble() * 15), 0, 100),
            MemoryUsage: Math.Clamp(45 + (random.NextDouble() * 30), 0, 100),
            ActiveConnections: random.Next(5, 50),
            RequestsPerSecond: Math.Round(100 + (random.NextDouble() * 200), 1),
            AverageResponseTime: Math.Round(15 + (random.NextDouble() * 50), 1),
            Uptime: FormatUptime(uptime.Elapsed));
    }

    private static NotificationSeverity DetermineSeverity(SystemMetrics metrics)
    {
        if (metrics.CpuUsage > 90 || metrics.MemoryUsage > 90)
        {
            return NotificationSeverity.Critical;
        }

        if (metrics.CpuUsage > 75 || metrics.MemoryUsage > 80)
        {
            return NotificationSeverity.Warning;
        }

        if (metrics.AverageResponseTime > 50)
        {
            return NotificationSeverity.Warning;
        }

        return NotificationSeverity.Info;
    }

    private static string GenerateMessage(
        NotificationSeverity severity,
        SystemMetrics metrics)
        => severity switch
        {
            NotificationSeverity.Critical =>
                $"CRITICAL: High resource usage - CPU: {metrics.CpuUsage:F1}%, Memory: {metrics.MemoryUsage:F1}%",
            NotificationSeverity.Warning =>
                $"Warning: Elevated resource usage detected - CPU: {metrics.CpuUsage:F1}%, Memory: {metrics.MemoryUsage:F1}%",
            NotificationSeverity.Error =>
                $"Error: System performance degraded - Response time: {metrics.AverageResponseTime:F1}ms",
            _ =>
                $"System healthy - {metrics.ActiveConnections} connections, {metrics.RequestsPerSecond:F1} req/s",
        };

    private async Task SendRandomAlertAsync(
        INotificationService notificationService)
    {
        var alerts = new[]
        {
            ("New user registered", NotificationType.User, NotificationSeverity.Info),
            ("Database backup completed", NotificationType.System, NotificationSeverity.Info),
            ("Cache cleared", NotificationType.System, NotificationSeverity.Info),
            ("API rate limit approaching", NotificationType.Alert, NotificationSeverity.Warning),
            ("Scheduled maintenance reminder", NotificationType.System, NotificationSeverity.Info),
        };

        var (message, type, severity) = alerts[random.Next(alerts.Length)];

        var alert = new SystemNotification(
            Id: Guid.NewGuid(),
            Type: type,
            Severity: severity,
            Timestamp: DateTimeOffset.UtcNow,
            Message: message,
            Data: null,
            Metrics: null!);

        await notificationService.SendSystemNotificationAsync(alert);
    }

    private static string FormatUptime(TimeSpan elapsed)
    {
        if (elapsed.TotalDays >= 1)
        {
            return $"{(int)elapsed.TotalDays}d {elapsed.Hours}h {elapsed.Minutes}m";
        }

        if (elapsed.TotalHours >= 1)
        {
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s";
        }

        return $"{elapsed.Minutes}m {elapsed.Seconds}s";
    }
}