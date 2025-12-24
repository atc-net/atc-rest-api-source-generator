namespace MultiParts.Api.Domain.Repositories;

public class ProductInMemoryRepository
{
    private readonly List<Product> products = [];

    public ProductInMemoryRepository()
    {
        // Seed with some initial data
        products.Add(new Product(Guid.NewGuid(), "Widget", "A useful widget", 19.99, "Electronics", true));
        products.Add(new Product(Guid.NewGuid(), "Gadget", "A cool gadget", 49.99, "Electronics", true));
        products.Add(new Product(Guid.NewGuid(), "Gizmo", "An amazing gizmo", 99.99, "Gadgets", false));
    }

    public Product[] GetAll(int? limit = null, int? offset = null)
    {
        var query = products.AsEnumerable();

        if (offset.HasValue)
        {
            query = query.Skip(offset.Value);
        }

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return query.ToArray();
    }

    public Product? GetById(Guid id) => products.FirstOrDefault(p => p.Id == id);

    public Product Add(
        string name,
        double price,
        string? description = null,
        string? category = null)
    {
        var newProduct = new Product(
            Guid.NewGuid(),
            name,
            description ?? string.Empty,
            price,
            category ?? string.Empty,
            true);
        products.Add(newProduct);
        return newProduct;
    }

    public bool Delete(Guid id)
    {
        var product = GetById(id);
        if (product is null)
        {
            return false;
        }

        products.Remove(product);
        return true;
    }
}
