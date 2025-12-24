namespace Showcase.Api.Domain.WebhookHandlers;

/// <summary>
/// Handles system notification webhooks by broadcasting to connected SignalR clients.
/// </summary>
public sealed class OnSystemNotificationWebhookHandler : IOnSystemNotificationWebhookHandler
{
    private readonly INotificationService notificationService;
    private readonly ILogger<OnSystemNotificationWebhookHandler> logger;

    public OnSystemNotificationWebhookHandler(
        INotificationService notificationService,
        ILogger<OnSystemNotificationWebhookHandler> logger)
    {
        this.notificationService = notificationService;
        this.logger = logger;
    }

    public async Task<OnSystemNotificationWebhookResult> ExecuteAsync(
        OnSystemNotificationWebhookParameters parameters,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Received system notification webhook: {Type} - {Message}",
            parameters.Payload.Type,
            parameters.Payload.Message);

        // Broadcast to all connected SignalR clients
        await notificationService.SendSystemNotificationAsync(parameters.Payload);

        return OnSystemNotificationWebhookResult.Ok();
    }
}