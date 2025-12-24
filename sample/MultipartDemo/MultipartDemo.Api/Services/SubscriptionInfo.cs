namespace MultipartDemo.Api.Services;

/// <summary>
/// Information about a subscription.
/// </summary>
public sealed record SubscriptionInfo(
    string Id,
    string ConnectionId,
    string[] Topics,
    DateTime CreatedAt,
    DateTime? LastNotificationAt);