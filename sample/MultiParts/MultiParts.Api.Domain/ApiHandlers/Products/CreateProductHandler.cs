namespace MultiParts.Api.Domain.ApiHandlers.Products;

public class CreateProductHandler : ICreateProductHandler
{
    private readonly ProductInMemoryRepository repository;

    public CreateProductHandler(ProductInMemoryRepository repository)
    {
        this.repository = repository;
    }

    public Task<CreateProductResult> ExecuteAsync(
        CreateProductParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameters.Request);

        var newProduct = repository.Add(
            parameters.Request.Name,
            parameters.Request.Price,
            parameters.Request.Description,
            parameters.Request.Category);

        return Task.FromResult(CreateProductResult.Created(newProduct));
    }
}
