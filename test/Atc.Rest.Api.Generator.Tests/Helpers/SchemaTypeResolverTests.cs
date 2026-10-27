namespace Atc.Rest.Api.Generator.Tests.Helpers;

public sealed class SchemaTypeResolverTests
{
    private const string Yaml = """
        openapi: 3.1.0
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
            Coordinate:
              type: array
              prefixItems:
                - { type: number }
                - { type: number }
            PaginatedResult:
              type: object
              properties:
                pageSize: { type: integer }
            ProductPage:
              allOf:
                - $ref: '#/components/schemas/PaginatedResult'
                - type: object
                  properties:
                    items: { type: array, items: { $ref: '#/components/schemas/Product' } }
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
    [InlineData("Coordinate", "Coordinate")]
    public void Resolve_AReference_IsItsTypeOrThePrimitiveItAliases(
        string component,
        string expected)
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var reference = new OpenApiSchemaReference(component, document);

        Assert.Equal(expected, SchemaTypeResolver.Resolve(reference, document, registry: null, collection: "List"));
    }

    [Fact]
    public void Resolve_AnArrayOfAPrimitiveAlias_IsACollectionOfThePrimitive()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var array = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchemaReference("ProductCode", document) };

        Assert.Equal("IEnumerable<string>", SchemaTypeResolver.Resolve(array, document, registry: null, collection: "IEnumerable"));
    }

    [Fact]
    public void Resolve_AnAllOfPage_IsThePageOfItsItems()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var page = document.Components!.Schemas!["ProductPage"];

        Assert.Equal("PaginatedResult<Product>", SchemaTypeResolver.Resolve(page, document, registry: null, collection: "List"));
    }

    [Fact]
    public void Resolve_AnInlineObjectAndItsArray_RegisterOneRecordPerContext()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var item = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["name"] = new OpenApiSchema { Type = JsonSchemaType.String } },
        };
        var array = new OpenApiSchema { Type = JsonSchemaType.Array, Items = item };
        var inlineSchemas = new Dictionary<string, InlineSchemaInfo>(StringComparer.Ordinal);
        var records = SchemaTypeResolver.InlineRecords(inlineSchemas, "listReports", "Reports", registry: null);

        // Act
        var first = SchemaTypeResolver.Resolve(array, document, registry: null, "List", context: "Response", inlineTypeName: records);
        var second = SchemaTypeResolver.Resolve(array, document, registry: null, "List", context: "Response", inlineTypeName: records);
        var error = SchemaTypeResolver.Resolve(item, document, registry: null, "List", context: "BadRequestError", inlineTypeName: records);

        // Assert
        Assert.Equal("List<ListReportsResponseItem>", first);
        Assert.Equal(first, second);
        Assert.Equal("ListReportsBadRequestError", error);
        Assert.Equal(["ListReportsBadRequestError", "ListReportsResponseItem"], inlineSchemas.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(inlineSchemas.Values, info => Assert.Equal("Reports", info.PathSegment));
    }

    [Fact]
    public void Resolve_WithoutAnOperationId_HasNoInlineRecord()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);
        var inline = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal) { ["id"] = new OpenApiSchema { Type = JsonSchemaType.String } },
        };
        var inlineSchemas = new Dictionary<string, InlineSchemaInfo>(StringComparer.Ordinal);

        var type = SchemaTypeResolver.Resolve(inline, document, registry: null, "List", context: "Response", inlineTypeName: SchemaTypeResolver.InlineRecords(inlineSchemas, operationId: null, "Reports", registry: null));

        Assert.Equal("object", type);
        Assert.Empty(inlineSchemas);
    }

    [Fact]
    public void Resolve_WithAModelNameRule_WritesEveryReferencedModelWithIt()
    {
        var document = OpenApiDocumentHelper.ParseYaml(Yaml);

        var type = SchemaTypeResolver.Resolve(
            new OpenApiSchemaReference("Products", document),
            document,
            registry: null,
            "List",
            modelName: name => "Shop.Models." + name);

        Assert.Equal("List<Shop.Models.Product>", type);
    }
}