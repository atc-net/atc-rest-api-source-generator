namespace Showcase.Api.Domain.WebhookHandlers;

/// <summary>
/// Handles data change webhooks by broadcasting to connected SignalR clients.
/// </summary>
public sealed class OnDataChangeWebhookHandler : IOnDataChangeWebhookHandler
{
    private readonly INotificationService notificationService;
    private readonly ILogger<OnDataChangeWebhookHandler> logger;

    public OnDataChangeWebhookHandler(
        INotificationService notificationService,
        ILogger<OnDataChangeWebhookHandler> logger)
    {
        this.notificationService = notificationService;
        this.logger = logger;
    }

    public async Task<OnDataChangeWebhookResult> ExecuteAsync(
        OnDataChangeWebhookParameters parameters,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Received data change webhook: {Operation} on {EntityType}/{EntityId}",
            parameters.Payload.Operation,
            parameters.Payload.EntityType,
            parameters.Payload.EntityId);

        // Broadcast to all connected SignalR clients
        await notificationService.SendDataChangeEventAsync(parameters.Payload);

        return OnDataChangeWebhookResult.Ok();
    }
}