namespace MultipartDemo.Api.Domain.ApiHandlers.Notifications;

/// <summary>
/// Handler business logic for the CreateSubscription operation.
/// </summary>
public sealed class CreateSubscriptionHandler : ICreateSubscriptionHandler
{
    public System.Threading.Tasks.Task<CreateSubscriptionResult> ExecuteAsync(
        CreateSubscriptionParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement createSubscription logic
        throw new NotImplementedException("createSubscription not implemented");
    }
}