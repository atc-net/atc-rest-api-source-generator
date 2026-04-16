namespace Showcase.Api.Domain.ApiHandlers.Notifications;

/// <summary>
/// Handler business logic for the CreateSubscription operation.
/// </summary>
public sealed class CreateSubscriptionHandler : ICreateSubscriptionHandler
{
    private static readonly ActivitySource ActivitySource = new("Showcase.Handlers.Notifications.CreateSubscription");
    private readonly SubscriptionInMemoryRepository repository;

    public CreateSubscriptionHandler(SubscriptionInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<CreateSubscriptionResult> ExecuteAsync(
        CreateSubscriptionParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("CreateSubscription");
        var subscription = repository.Create(parameters.Request);
        return Task.FromResult(CreateSubscriptionResult.Created(subscription));
    }
}