namespace Polymorphism.ApiHandlers;

/// <summary>
/// Handler business logic for the ListNotifications operation.
/// </summary>
public sealed class ListNotificationsHandler : IListNotificationsHandler
{
    public Task<ListNotificationsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listNotifications logic
        throw new NotImplementedException("listNotifications not implemented");
    }
}