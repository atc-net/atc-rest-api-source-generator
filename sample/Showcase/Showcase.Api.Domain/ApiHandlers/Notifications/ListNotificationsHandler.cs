namespace Showcase.Api.Domain.ApiHandlers.Notifications;

/// <summary>
/// Handler business logic for the ListNotifications operation.
/// Provides Server-Sent Events (SSE) for real-time notifications.
/// </summary>
public sealed class ListNotificationsHandler : IListNotificationsHandler
{
    private readonly Random random = new();

    public Task<ListNotificationsResult> ExecuteAsync(
        ListNotificationsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var topics = ParseTopics(parameters.Topics);
        var stream = StreamNotificationsAsync(topics, cancellationToken);
        return Task.FromResult(ListNotificationsResult.Ok(stream));
    }

    private static NotificationType[] ParseTopics(string? topicsString)
    {
        if (string.IsNullOrEmpty(topicsString))
        {
            // Default to all topics
            return
            [
                NotificationType.System,
                NotificationType.User,
                NotificationType.Data,
            ];
        }

        var topicNames = topicsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var topics = new List<NotificationType>();

        foreach (var name in topicNames)
        {
            if (Enum.TryParse<NotificationType>(name, ignoreCase: true, out var topic))
            {
                topics.Add(topic);
            }
        }

        return topics.Count > 0
            ? topics.ToArray()
            : [NotificationType.System, NotificationType.User, NotificationType.Data];
    }

    private async IAsyncEnumerable<NotificationEvent> StreamNotificationsAsync(
        NotificationType[] topics,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var messageCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Wait 5-10 seconds between events
            await Task.Delay(TimeSpan.FromSeconds(5 + (random.NextDouble() * 5)), cancellationToken);

            messageCount++;

            // Select a random topic from the subscribed topics
            var topic = topics[random.Next(topics.Length)];

            var notification = CreateNotificationEvent(topic, messageCount);
            yield return notification;
        }
    }

    private NotificationEvent CreateNotificationEvent(
        NotificationType type,
        int messageNumber)
    {
        var severity = DetermineSeverity();
        var message = GenerateMessage(type, severity, messageNumber);

        return new NotificationEvent(
            Id: Guid.NewGuid(),
            Type: type,
            Timestamp: DateTimeOffset.UtcNow,
            Message: message,
            Severity: severity,
            Payload: null!);
    }

    private NotificationSeverity DetermineSeverity()
    {
        var roll = random.Next(100);
        return roll switch
        {
            < 5 => NotificationSeverity.Critical,
            < 15 => NotificationSeverity.Warning,
            < 20 => NotificationSeverity.Error,
            _ => NotificationSeverity.Info,
        };
    }

    private static string GenerateMessage(
        NotificationType type,
        NotificationSeverity severity,
        int number)
        => type switch
        {
            NotificationType.System => severity switch
            {
                NotificationSeverity.Critical => $"[#{number}] CRITICAL: System resources at capacity",
                NotificationSeverity.Warning => $"[#{number}] Warning: High memory usage detected",
                NotificationSeverity.Error => $"[#{number}] Error: Service connection timeout",
                _ => $"[#{number}] System heartbeat - all services operational",
            },
            NotificationType.User => severity switch
            {
                NotificationSeverity.Warning => $"[#{number}] User session about to expire",
                _ => $"[#{number}] User activity logged",
            },
            NotificationType.Data => severity switch
            {
                NotificationSeverity.Warning => $"[#{number}] Large data modification detected",
                _ => $"[#{number}] Data sync completed successfully",
            },
            NotificationType.Alert => $"[#{number}] Alert: {severity} condition detected",
            NotificationType.Metric => $"[#{number}] Metric update: Performance data collected",
            _ => $"[#{number}] Notification event",
        };
}