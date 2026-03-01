namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class EnumExtractorTests
{
    [Fact]
    public void Extract_WithStringEnum_ReturnsEnumParameters()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Pet'
                            components:
                              schemas:
                                PetStatus:
                                  type: string
                                  enum:
                                    - Available
                                    - Pending
                                    - Sold
                                Pet:
                                  type: object
                                  properties:
                                    status:
                                      $ref: '#/components/schemas/PetStatus'
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var result = EnumExtractor.Extract(document!, "TestProject", "Pets");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        var enumParams = result[0];
        Assert.Equal("PetStatus", enumParams.EnumTypeName);
        Assert.Equal(3, enumParams.Values.Count);
        Assert.Contains(enumParams.Values, v => v.Name == "Available");
        Assert.Contains(enumParams.Values, v => v.Name == "Pending");
        Assert.Contains(enumParams.Values, v => v.Name == "Sold");
    }

    [Fact]
    public void ExtractForSchemas_NoSchemas_ReturnsNull()
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

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "NonExistent" };

        // Act
        var result = EnumExtractor.ExtractForSchemas(document!, "TestProject", schemaNames, null);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("-")]
    [InlineData(":")]
    [InlineData("_")]
    [InlineData(" ")]
    [InlineData("*")]
    public void NeedsEnumMemberAttribute_SpecialChars_ReturnsTrue(
        string specialChar)
    {
        // Arrange
        var value = $"test{specialChar}value";

        // Act
        var result = EnumExtractor.NeedsEnumMemberAttribute(value);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void NeedsEnumMemberAttribute_LowercaseFirst_ReturnsTrue()
    {
        // Act
        var result = EnumExtractor.NeedsEnumMemberAttribute("camelCase");

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("PascalCase")]
    [InlineData("Active")]
    [InlineData("OK")]
    public void NeedsEnumMemberAttribute_PascalCase_ReturnsFalse(string value)
    {
        // Act
        var result = EnumExtractor.NeedsEnumMemberAttribute(value);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NeedsEnumMemberAttribute_NullOrEmpty_ReturnsFalse(string? value)
    {
        // Act
        var result = EnumExtractor.NeedsEnumMemberAttribute(value!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GenerateEnumContent_ProducesValidCSharp()
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
                                Color:
                                  type: string
                                  enum:
                                    - Red
                                    - Green
                                    - Blue
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Color" };
        var result = EnumExtractor.ExtractForSchemas(document!, "TestProject", schemaNames, null);
        Assert.NotNull(result);
        Assert.Single(result);

        // Act
        var content = EnumExtractor.GenerateEnumContent(result[0]);

        // Assert
        Assert.Contains("GeneratedCode", content, StringComparison.Ordinal);
        Assert.Contains("enum Color", content, StringComparison.Ordinal);
        Assert.Contains("Red", content, StringComparison.Ordinal);
        Assert.Contains("Green", content, StringComparison.Ordinal);
        Assert.Contains("Blue", content, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateEnumContent_WithEnumMemberValues_IncludesAttribute()
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
                                HttpMethod:
                                  type: string
                                  enum:
                                    - get-request
                                    - post-request
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "HttpMethod" };
        var result = EnumExtractor.ExtractForSchemas(document!, "TestProject", schemaNames, null);
        Assert.NotNull(result);
        Assert.Single(result);

        // Act
        var content = EnumExtractor.GenerateEnumContent(result[0]);

        // Assert
        Assert.Contains("EnumMember", content, StringComparison.Ordinal);
        Assert.Contains("get-request", content, StringComparison.Ordinal);
        Assert.Contains("post-request", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}