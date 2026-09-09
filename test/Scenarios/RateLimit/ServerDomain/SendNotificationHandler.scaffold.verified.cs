namespace RateLimit.ApiHandlers;

/// <summary>
/// Handler business logic for the SendNotification operation.
/// </summary>
public sealed class SendNotificationHandler : ISendNotificationHandler
{
    public Task<SendNotificationResult> ExecuteAsync(
        SendNotificationParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement sendNotification logic
        throw new NotImplementedException("sendNotification not implemented");
    }
}