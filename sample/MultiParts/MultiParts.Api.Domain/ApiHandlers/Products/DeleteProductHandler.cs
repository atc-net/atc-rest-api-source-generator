namespace MultiParts.Api.Domain.ApiHandlers.Products;

public class DeleteProductHandler : IDeleteProductHandler
{
    private readonly ProductInMemoryRepository repository;

    public DeleteProductHandler(ProductInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<DeleteProductResult> ExecuteAsync(
        DeleteProductParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var deleted = repository.Delete(parameters.ProductId);
        if (!deleted)
        {
            return Task.FromResult(DeleteProductResult.NotFound());
        }

        return Task.FromResult(DeleteProductResult.NoContent());
    }
}
