namespace Showcase.Api.Domain.ApiHandlers.Notifications;

/// <summary>
/// Handler business logic for the GetSubscriptionById operation.
/// </summary>
public sealed class GetSubscriptionByIdHandler : IGetSubscriptionByIdHandler
{
    private readonly SubscriptionInMemoryRepository repository;

    public GetSubscriptionByIdHandler(SubscriptionInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<GetSubscriptionByIdResult> ExecuteAsync(
        GetSubscriptionByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var subscription = repository.GetById(parameters.SubscriptionId);

        if (subscription is null)
        {
            return Task.FromResult(GetSubscriptionByIdResult.NotFound());
        }

        return Task.FromResult(GetSubscriptionByIdResult.Ok(subscription));
    }
}