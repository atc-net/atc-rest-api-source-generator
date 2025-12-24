namespace MultiParts.Api.Domain.ApiHandlers.Products;

public class GetProductByIdHandler : IGetProductByIdHandler
{
    private readonly ProductInMemoryRepository repository;

    public GetProductByIdHandler(ProductInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<GetProductByIdResult> ExecuteAsync(
        GetProductByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var product = repository.GetById(parameters.ProductId);
        if (product is null)
        {
            return Task.FromResult(GetProductByIdResult.NotFound());
        }

        return Task.FromResult(GetProductByIdResult.Ok(product));
    }
}
