namespace Showcase.Api.Domain.ApiHandlers.Notifications;

/// <summary>
/// Handler business logic for the CreateSubscription operation.
/// </summary>
public sealed class CreateSubscriptionHandler : ICreateSubscriptionHandler
{
    private readonly SubscriptionInMemoryRepository repository;

    public CreateSubscriptionHandler(SubscriptionInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<CreateSubscriptionResult> ExecuteAsync(
        CreateSubscriptionParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var subscription = repository.Create(parameters.Request);
        return Task.FromResult(CreateSubscriptionResult.Created(subscription));
    }
}