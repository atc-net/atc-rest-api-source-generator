namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class TupleExtractorTests
{
    [Fact]
    public void Extract_WithPrefixItems_ReturnsTupleRecord()
    {
        // Arrange — build a document with a schema that has prefixItems in Extensions
        var document = BuildDocumentWithTupleSchema(
            "Coordinate",
            "A geographic coordinate",
            new JsonArray(
                new JsonObject { ["type"] = "number", ["description"] = "Latitude" },
                new JsonObject { ["type"] = "number", ["description"] = "Longitude" }));

        // Act
        var result = TupleExtractor.Extract(document, "TestProject");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        var record = result[0];
        Assert.Equal("Coordinate", record.Name);
        Assert.NotNull(record.Parameters);
        Assert.Equal(2, record.Parameters.Count);
    }

    [Fact]
    public void Extract_NoPrefixItems_ReturnsNull()
    {
        // Arrange — regular object schema without prefixItems
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                User:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var result = TupleExtractor.Extract(document!, "TestProject");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Extract_NoSchemas_ReturnsNull()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var result = TupleExtractor.Extract(document!, "TestProject");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void IsTupleSchema_WithPrefixItems_ReturnsTrue()
    {
        // Arrange — construct a schema with prefixItems extension
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["prefixItems"] = new JsonNodeExtension(
                    new JsonArray(
                        new JsonObject { ["type"] = "number" },
                        new JsonObject { ["type"] = "number" })),
            },
        };

        // Act
        var result = TupleExtractor.IsTupleSchema(schema);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsTupleSchema_WithoutPrefixItems_ReturnsFalse()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Names:
                                  type: array
                                  items:
                                    type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var schema = document!.Components!.Schemas!["Names"];

        // Act
        var result = TupleExtractor.IsTupleSchema(schema);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Extract_DeprecatedSchema_SkippedByDefault()
    {
        // Arrange — build a deprecated tuple schema
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Deprecated = true,
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["prefixItems"] = new JsonNodeExtension(
                    new JsonArray(
                        new JsonObject { ["type"] = "number" },
                        new JsonObject { ["type"] = "number" })),
            },
        };

        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test", Version = "1.0" },
            Paths = new OpenApiPaths(),
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                {
                    ["OldCoordinate"] = schema,
                },
            },
        };

        // Act
        var result = TupleExtractor.Extract(document, "TestProject");

        // Assert
        Assert.Null(result);
    }

    private static OpenApiDocument BuildDocumentWithTupleSchema(
        string schemaName,
        string description,
        JsonArray prefixItemsArray)
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Description = description,
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["prefixItems"] = new JsonNodeExtension(prefixItemsArray),
            },
        };

        return new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test", Version = "1.0" },
            Paths = new OpenApiPaths(),
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                {
                    [schemaName] = schema,
                },
            },
        };
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}