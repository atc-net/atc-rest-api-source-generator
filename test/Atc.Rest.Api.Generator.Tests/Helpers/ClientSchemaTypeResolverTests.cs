namespace Atc.Rest.Api.Generator.Tests.Helpers;

public sealed class ClientSchemaTypeResolverTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info: { title: Shop, version: 1.0.0 }
        paths: {}
        components:
          schemas:
            ProductCode: { type: string, pattern: '^[A-Z]{2}\d{4}$' }
            ProductId: { type: string, format: uuid }
            Product:
              type: object
              properties:
                code: { $ref: '#/components/schemas/ProductCode' }
            Products: { type: array, items: { $ref: '#/components/schemas/Product' } }
            Codes: { type: array, items: { $ref: '#/components/schemas/ProductCode' } }
        """;

    /// <summary>
    /// A primitive alias (a component that is only a string, with a pattern or a format) is no generated type: a body
    /// that references it - alone, as an array's items or through an array alias - is the primitive.
    /// </summary>
    [Theory]
    [InlineData("ProductCode", "string")]
    [InlineData("ProductId", "Guid")]
    [InlineData("Product", "Product")]
    [InlineData("Products", "List<Product>")]
    [InlineData("Codes", "List<string>")]
    public void Resolve_AReference_IsItsTypeOrThePrimitiveItAliases(
        string component,
        string expected)
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var reference = new OpenApiSchemaReference(component, document);

        Assert.Equal(expected, ClientSchemaTypeResolver.Resolve(reference, document, registry: null, collection: "List"));
    }

    [Fact]
    public void Resolve_AnArrayOfAPrimitiveAlias_IsACollectionOfThePrimitive()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var array = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchemaReference("ProductCode", document) };

        Assert.Equal("IEnumerable<string>", ClientSchemaTypeResolver.Resolve(array, document, registry: null, collection: "IEnumerable"));
    }
}