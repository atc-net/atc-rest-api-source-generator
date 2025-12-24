namespace MultiParts.Api.Domain.ApiHandlers.Products;

public class ListProductsHandler : IListProductsHandler
{
    private readonly ProductInMemoryRepository repository;

    public ListProductsHandler(ProductInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<ListProductsResult> ExecuteAsync(
        ListProductsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var products = repository.GetAll(parameters.Limit, parameters.Offset);
        return Task.FromResult(ListProductsResult.Ok(products));
    }
}
