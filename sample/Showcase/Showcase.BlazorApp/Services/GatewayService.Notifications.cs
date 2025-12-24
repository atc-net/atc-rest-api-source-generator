namespace Showcase.BlazorApp.Services;

/// <summary>
/// Gateway service - Notifications operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// List all notification subscriptions.
    /// </summary>
    public async Task<Subscription[]?> ListSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await listSubscriptionsEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.ToArray()
            : null;
    }

    /// <summary>
    /// Create a new subscription.
    /// </summary>
    public async Task<Subscription?> CreateSubscriptionAsync(
        CreateSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateSubscriptionParameters(Request: request);
        var result = await createSubscriptionEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsCreated
            ? result.CreatedContent
            : null;
    }

    /// <summary>
    /// Get a subscription by ID.
    /// </summary>
    public async Task<Subscription?> GetSubscriptionByIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new GetSubscriptionByIdParameters(SubscriptionId: subscriptionId);
        var result = await getSubscriptionByIdEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : null;
    }

    /// <summary>
    /// Delete a subscription by ID.
    /// </summary>
    public async Task<bool> DeleteSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DeleteSubscriptionParameters(SubscriptionId: subscriptionId);
        var result = await deleteSubscriptionEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsNoContent;
    }
}