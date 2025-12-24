namespace Showcase.Api.Domain.WebhookHandlers;

/// <summary>
/// Extension methods for registering webhook handlers.
/// </summary>
public static class WebhookHandlerServiceCollectionExtensions
{
    /// <summary>
    /// Registers all webhook handler implementations.
    /// </summary>
    public static IServiceCollection AddWebhookHandlersFromDomain(
        this IServiceCollection services)
    {
        services.AddScoped<IOnSystemNotificationWebhookHandler, OnSystemNotificationWebhookHandler>();
        services.AddScoped<IOnUserActivityWebhookHandler, OnUserActivityWebhookHandler>();
        services.AddScoped<IOnDataChangeWebhookHandler, OnDataChangeWebhookHandler>();

        return services;
    }
}