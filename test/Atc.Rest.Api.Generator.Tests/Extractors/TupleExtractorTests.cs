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
    public void GetTupleInfo_PrefixItemWithMalformedRef_FallsBackToSafeType()
    {
        // Arrange — a prefixItems entry whose $ref has an empty last segment (trailing slash).
        // Naively splitting on '/' and taking Last() yields "", which would produce an empty,
        // invalid C# type in the generated tuple. The parser must fall back to a safe type.
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["prefixItems"] = new JsonNodeExtension(
                    new JsonArray(
                        new JsonObject { ["$ref"] = "#/components/schemas/" })),
            },
        };

        // Act
        var result = schema.GetTupleInfo();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.PrefixItems);
        Assert.False(string.IsNullOrWhiteSpace(result.PrefixItems[0].CSharpType));
        Assert.Equal("object", result.PrefixItems[0].CSharpType);
    }

    /// <summary>
    /// A keyword of a prefixItems entry with a value that is no string (a number as its type, format, description or
    /// $ref) is ignored, not thrown on: the generators read these entries as raw JSON.
    /// </summary>
    [Theory]
    [InlineData("type")]
    [InlineData("format")]
    [InlineData("description")]
    [InlineData("$ref")]
    public void GetTupleInfo_PrefixItemKeywordThatIsNoString_IsIgnored(
        string keyword)
    {
        var item = new JsonObject { ["type"] = "number", [keyword] = 42 };
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["prefixItems"] = new JsonNodeExtension(new JsonArray(item)),
            },
        };

        var result = schema.GetTupleInfo();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(result.PrefixItems).CSharpType));
        IOpenApiSchema typeScriptSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["prefixItems"] = new JsonNodeExtension(new JsonArray(item.DeepClone())),
            },
        };
        Assert.False(string.IsNullOrWhiteSpace(typeScriptSchema.ToTypeScriptTypeForModel(isRequired: true)));
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
        var result = TupleExtractor.Extract(document, "TestProject");

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
        var result = TupleExtractor.Extract(document, "TestProject");

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

        var schema = document.Components.Schemas["Names"];

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

    [Fact]
    public void ExtractForSchemas_FiltersToTheRequestedSchemas()
    {
        // Arrange — two tuple schemas, only one requested
        var document = BuildDocumentWithTupleSchemas(
            ("Coordinate", "A geographic coordinate", new JsonArray(
                new JsonObject { ["type"] = "number", ["description"] = "Latitude" },
                new JsonObject { ["type"] = "number", ["description"] = "Longitude" })),
            ("Range", "A range", new JsonArray(
                new JsonObject { ["type"] = "integer" },
                new JsonObject { ["type"] = "integer" })));

        // Act
        var result = TupleExtractor.ExtractForSchemas(
            document,
            "TestProject",
            new HashSet<string>(StringComparer.Ordinal) { "Coordinate" },
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        var record = Assert.Single(result);
        Assert.Equal("Coordinate", record.Name);
        Assert.Equal(DeclarationModifiers.PublicSealedRecord, record.DeclarationModifier);
        Assert.Equal("A geographic coordinate", record.DocumentationTags!.Summary);
        Assert.Equal(["Latitude", "Longitude"], record.Parameters!.Select(p => p.Name), StringComparer.Ordinal);
        Assert.All(record.Parameters!, p => Assert.Equal("double", p.TypeName));
        Assert.Contains(record.Attributes!, a => a.Name == "JsonConverter" && a.Content == "typeof(CoordinateJsonConverter)");
    }

    [Fact]
    public void ExtractForSchemas_EmptyFilterOrNoTupleMatch_ReturnsNull()
    {
        // Arrange
        var document = BuildDocumentWithTupleSchema(
            "Coordinate",
            "A geographic coordinate",
            new JsonArray(
                new JsonObject { ["type"] = "number" },
                new JsonObject { ["type"] = "number" }));

        // Act & Assert
        Assert.Null(TupleExtractor.ExtractForSchemas(document, "TestProject", new HashSet<string>(StringComparer.Ordinal), pathSegment: null));
        Assert.Null(TupleExtractor.ExtractForSchemas(document, "TestProject", new HashSet<string>(StringComparer.Ordinal) { "Other" }, pathSegment: null));
    }

    [Fact]
    public void ExtractForSchemas_DeprecatedTuple_IncludedOnlyWhenRequested()
    {
        // Arrange
        var document = BuildDocumentWithTupleSchema(
            "OldCoordinate",
            "Deprecated",
            new JsonArray(
                new JsonObject { ["type"] = "number" },
                new JsonObject { ["type"] = "number" }));
        ((OpenApiSchema)document.Components!.Schemas!["OldCoordinate"]).Deprecated = true;
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "OldCoordinate" };

        // Act
        var skipped = TupleExtractor.ExtractForSchemas(document, "TestProject", schemaNames, pathSegment: null);
        var included = TupleExtractor.ExtractForSchemas(document, "TestProject", schemaNames, pathSegment: null, includeDeprecated: true);

        // Assert
        Assert.Null(skipped);
        Assert.Equal("OldCoordinate", Assert.Single(included!).Name);
    }

    [Fact]
    public void GenerateTupleContent_StrictTuple_EmitsRecordAndJsonArrayConverter()
    {
        // Arrange
        var document = BuildDocumentWithTupleSchema(
            "Coordinate",
            "A geographic coordinate",
            new JsonArray(
                new JsonObject { ["type"] = "number", ["description"] = "Latitude" },
                new JsonObject { ["type"] = "number", ["description"] = "Longitude" }));
        var record = TupleExtractor.Extract(document, "TestProject")![0];

        // Act
        var content = TupleExtractor.GenerateTupleContent(record, "TestProject.Generated.Models");

        // Assert
        Assert.Contains("using System.Text.Json.Serialization;", content, StringComparison.Ordinal);
        Assert.Contains("namespace TestProject.Generated.Models;", content, StringComparison.Ordinal);
        Assert.Contains("[JsonConverter(typeof(CoordinateJsonConverter))]", content, StringComparison.Ordinal);
        Assert.Contains("record Coordinate(", content, StringComparison.Ordinal);
        Assert.Contains("double Latitude", content, StringComparison.Ordinal);
        Assert.Contains("double Longitude", content, StringComparison.Ordinal);

        Assert.Contains("public sealed class CoordinateJsonConverter : JsonConverter<Coordinate>", content, StringComparison.Ordinal);
        Assert.Contains("throw new JsonException(\"A Coordinate is a JSON array.\");", content, StringComparison.Ordinal);
        Assert.Contains("throw new JsonException(\"A Coordinate has 2 item(s); item 1 (Latitude) is missing.\");", content, StringComparison.Ordinal);
        Assert.Contains("var item0 = JsonSerializer.Deserialize<double>(ref reader, options)!;", content, StringComparison.Ordinal);
        Assert.Contains("var item1 = JsonSerializer.Deserialize<double>(ref reader, options)!;", content, StringComparison.Ordinal);
        Assert.Contains("// Items beyond the ones the tuple names are not part of the model", content, StringComparison.Ordinal);
        Assert.Contains("return new Coordinate(item0, item1);", content, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Serialize(writer, value.Latitude, options);", content, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Serialize(writer, value.Longitude, options);", content, StringComparison.Ordinal);
        Assert.EndsWith("}", content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateTupleContent_OpenTuple_ReadsAndWritesAdditionalItems()
    {
        // Arrange — prefixItems plus an items schema: the trailing values are kept
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { Type = JsonSchemaType.String },
            Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
            {
                ["prefixItems"] = new JsonNodeExtension(
                    new JsonArray(
                        new JsonObject { ["type"] = "integer", ["description"] = "Code" })),
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
                    ["Entry"] = schema,
                },
            },
        };

        var record = TupleExtractor.Extract(document, "TestProject")![0];
        var additional = record.Parameters!.Single(p => p.Name == "AdditionalItems");
        Assert.Equal("string[]", additional.TypeName);

        // Act
        var content = TupleExtractor.GenerateTupleContent(record, "TestProject.Generated.Models");

        // Assert
        Assert.Contains("var additionalItems = new List<string>();", content, StringComparison.Ordinal);
        Assert.Contains("additionalItems.Add(JsonSerializer.Deserialize<string>(ref reader, options)!);", content, StringComparison.Ordinal);
        Assert.Contains("return new Entry(item0, additionalItems.ToArray());", content, StringComparison.Ordinal);
        Assert.Contains("foreach (var item in value.AdditionalItems ?? [])", content, StringComparison.Ordinal);
        Assert.DoesNotContain("reader.Skip();", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument BuildDocumentWithTupleSchemas(
        params (string Name, string Description, JsonArray PrefixItems)[] schemas)
    {
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test", Version = "1.0" },
            Paths = new OpenApiPaths(),
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal),
            },
        };

        foreach (var (name, description, prefixItems) in schemas)
        {
            document.Components.Schemas[name] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Description = description,
                Extensions = new Dictionary<string, IOpenApiExtension>(StringComparer.Ordinal)
                {
                    ["prefixItems"] = new JsonNodeExtension(prefixItems),
                },
            };
        }

        return document;
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