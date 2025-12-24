namespace Showcase.Api.Contracts.Services;

/// <summary>
/// Interface for the notification service.
/// </summary>
public interface INotificationService
{
    Task<string> SubscribeAsync(
        string connectionId,
        string[] topics);

    Task<string[]> UnsubscribeAsync(string subscriptionId);

    Task UnsubscribeAllAsync(string connectionId);

    Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptionsAsync(
        string connectionId);

    Task<IReadOnlyList<SubscriptionInfo>> GetAllSubscriptionsAsync();

    Task SendSystemNotificationAsync(SystemNotification notification);

    Task SendUserActivityEventAsync(UserActivityEvent activityEvent);

    Task SendDataChangeEventAsync(DataChangeEvent changeEvent);
}