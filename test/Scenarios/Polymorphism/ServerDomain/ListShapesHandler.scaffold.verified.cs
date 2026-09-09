namespace Polymorphism.ApiHandlers;

/// <summary>
/// Handler business logic for the ListShapes operation.
/// </summary>
public sealed class ListShapesHandler : IListShapesHandler
{
    public Task<ListShapesResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement listShapes logic
        throw new NotImplementedException("listShapes not implemented");
    }
}