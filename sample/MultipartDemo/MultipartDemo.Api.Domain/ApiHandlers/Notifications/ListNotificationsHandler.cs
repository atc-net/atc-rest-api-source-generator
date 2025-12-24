namespace MultipartDemo.Api.Domain.ApiHandlers.Notifications;

/// <summary>
/// Handler business logic for the ListNotifications operation.
/// </summary>
public sealed class ListNotificationsHandler : IListNotificationsHandler
{
    public System.Threading.Tasks.Task<ListNotificationsResult> ExecuteAsync(
        ListNotificationsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listNotifications logic
        throw new NotImplementedException("listNotifications not implemented");
    }
}