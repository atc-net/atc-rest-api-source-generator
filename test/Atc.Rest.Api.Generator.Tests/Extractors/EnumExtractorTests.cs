namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class EnumExtractorTests
{
    private const string HostileEnumsYaml = """
        openapi: 3.0.0
        info: { title: Test API, version: 1.0.0 }
        paths:
          /tickets:
            get:
              operationId: listTickets
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Ticket' }
        components:
          schemas:
            Status:
              enum: [active, archived]
            Ratio:
              type: number
              enum: [0.5, 1.5]
            Flag:
              type: boolean
              enum: [true]
            Level:
              type: integer
              enum: [1, 2, 2, 3]
              x-enum-varnames: [Low, Medium, MediumAgain, High]
            Mark:
              type: string
              enum: ['', 'a', '2nd']
            Ticket:
              type: object
              properties:
                status: { $ref: '#/components/schemas/Status' }
                ratio: { $ref: '#/components/schemas/Ratio' }
                flag: { $ref: '#/components/schemas/Flag' }
                level: { $ref: '#/components/schemas/Level' }
                mark: { $ref: '#/components/schemas/Mark' }
        """;

    /// <summary>
    /// An enum without a <c>type</c> (JSON Schema needs none) is the enum its members say: strings make a string enum.
    /// </summary>
    [Fact]
    public void Extract_AnEnumWithoutAType_IsAStringEnum()
    {
        var result = EnumExtractor.Extract(ParseYaml(HostileEnumsYaml)!, "TestProject", "Tickets");

        var status = Assert.Single(result!, e => e.EnumTypeName == "Status");
        Assert.Equal(["Active", "Archived"], status.Values.Select(v => v.Name), StringComparer.Ordinal);
        Assert.Equal(["active", "archived"], status.Values.Select(v => v.EnumMemberValue), StringComparer.Ordinal);
    }

    /// <summary>
    /// A number or boolean enum is no C# enum (nothing holds a double): it is not extracted, and a property that
    /// references it is the primitive.
    /// </summary>
    [Fact]
    public void Extract_ANumberOrBooleanEnum_IsItsPrimitive_NotAnEnum()
    {
        var document = ParseYaml(HostileEnumsYaml)!;

        var result = EnumExtractor.Extract(document, "TestProject", "Tickets");
        var ticket = (OpenApiSchema)document.Components!.Schemas!["Ticket"];

        Assert.DoesNotContain(result!, e => e.EnumTypeName is "Ratio" or "Flag");
        Assert.Equal("double?", ticket.Properties!["ratio"].ToCSharpTypeForModel(isRequired: false));
        Assert.Equal("bool?", ticket.Properties["flag"].ToCSharpTypeForModel(isRequired: false));
        Assert.Equal("Status?", ticket.Properties["status"].ToCSharpTypeForModel(isRequired: false));
    }

    /// <summary>
    /// <c>x-enum-varnames</c> names the members by position, and a value listed twice is one member.
    /// </summary>
    [Fact]
    public void Extract_AnIntegerEnumWithVarnamesAndADuplicate_NamesTheMembers_Once()
    {
        var result = EnumExtractor.Extract(ParseYaml(HostileEnumsYaml)!, "TestProject", "Tickets");

        var level = Assert.Single(result!, e => e.EnumTypeName == "Level");
        Assert.Equal([("Low", 1L), ("Medium", 2L), ("High", 3L)], level.Values.Select(v => (v.Name, v.Value!.Value)));
    }

    /// <summary>
    /// The empty string is a value a specification may list: the member <c>Empty</c> carries it on the wire.
    /// </summary>
    [Fact]
    public void Extract_AnEmptyStringMember_IsTheMemberEmpty()
    {
        var result = EnumExtractor.Extract(ParseYaml(HostileEnumsYaml)!, "TestProject", "Tickets");

        var mark = Assert.Single(result!, e => e.EnumTypeName == "Mark");
        var empty = Assert.Single(mark.Values, v => v.Name == "Empty");
        Assert.Equal(string.Empty, empty.EnumMemberValue);
        Assert.Equal(3, mark.Values.Count);
    }

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
        var result = EnumExtractor.Extract(document, "TestProject", "Pets");

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
        var result = EnumExtractor.ExtractForSchemas(document, "TestProject", schemaNames, null);

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
        var result = EnumExtractor.NeedsEnumMemberAttribute(value);

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
        var result = EnumExtractor.ExtractForSchemas(document, "TestProject", schemaNames, null);
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
        var result = EnumExtractor.ExtractForSchemas(document, "TestProject", schemaNames, null);
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