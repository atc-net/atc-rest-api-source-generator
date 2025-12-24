namespace Showcase.Api.Domain.WebhookHandlers;

/// <summary>
/// Handles user activity webhooks by broadcasting to connected SignalR clients.
/// </summary>
public sealed class OnUserActivityWebhookHandler : IOnUserActivityWebhookHandler
{
    private readonly INotificationService notificationService;
    private readonly ILogger<OnUserActivityWebhookHandler> logger;

    public OnUserActivityWebhookHandler(
        INotificationService notificationService,
        ILogger<OnUserActivityWebhookHandler> logger)
    {
        this.notificationService = notificationService;
        this.logger = logger;
    }

    public async Task<OnUserActivityWebhookResult> ExecuteAsync(
        OnUserActivityWebhookParameters parameters,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Received user activity webhook: User {UserId} - {Action}",
            parameters.Payload.UserId,
            parameters.Payload.Action);

        // Broadcast to all connected SignalR clients
        await notificationService.SendUserActivityEventAsync(parameters.Payload);

        return OnUserActivityWebhookResult.Ok();
    }
}