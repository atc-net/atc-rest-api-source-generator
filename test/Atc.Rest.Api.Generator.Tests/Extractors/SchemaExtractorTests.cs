namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for SchemaExtractor, particularly header content generation with usings.
/// </summary>
public class SchemaExtractorTests
{
    [Fact]
    public void ExtractForSchemas_WithListProperty_IncludesCollectionsUsing()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            components:
              schemas:
                TestResponse:
                  type: object
                  properties:
                    items:
                      type: array
                      items:
                        type: string
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System.Collections.Generic;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithGuidProperty_IncludesSystemUsing()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            components:
              schemas:
                TestResponse:
                  type: object
                  properties:
                    id:
                      type: string
                      format: uuid
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithDateTimeOffsetProperty_IncludesSystemUsing()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            components:
              schemas:
                TestResponse:
                  type: object
                  properties:
                    createdAt:
                      type: string
                      format: date-time
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithListOfGuids_IncludesBothUsings()
    {
        // Arrange - This is the D365TestClient case: List<Guid> Ids
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            components:
              schemas:
                ResendEventsResponse:
                  type: object
                  properties:
                    resourceType:
                      type: string
                    processedCount:
                      type: integer
                    message:
                      type: string
                    ids:
                      type: array
                      items:
                        type: string
                        format: uuid
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "ResendEventsResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System;", result.HeaderContent, StringComparison.Ordinal);
        Assert.Contains("using System.Collections.Generic;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithUriProperty_IncludesSystemUsing()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            components:
              schemas:
                TestResponse:
                  type: object
                  properties:
                    callbackUrl:
                      type: string
                      format: uri
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithDictionaryProperty_IncludesCollectionsUsing()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            components:
              schemas:
                TestResponse:
                  type: object
                  properties:
                    metadata:
                      type: object
                      additionalProperties:
                        type: string
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("using System.Collections.Generic;", result.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithSimpleStringProperty_DoesNotIncludeSystemUsing()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            components:
              schemas:
                TestResponse:
                  type: object
                  properties:
                    name:
                      type: string
                    count:
                      type: integer
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestResponse" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("using System;", result.HeaderContent, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Collections.Generic;", result.HeaderContent, StringComparison.Ordinal);
    }

    // ========== x-implements Extension Tests ==========
    [Fact]
    public void ExtractForSchemas_WithXImplements_IncludesInterfaceInRecordName()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  x-implements:
                                    - IAnimal
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Parameters);
        Assert.Contains(" : IAnimal", result.Parameters[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithMultipleXImplements_IncludesAllInterfaces()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  x-implements:
                                    - IAnimal
                                    - ISerializable
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Parameters);
        Assert.Contains(" : IAnimal, ISerializable", result.Parameters[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithoutXImplements_DoesNotIncludeInterface()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Parameters);
        Assert.Equal("Pet", result.Parameters[0].Name);
    }

    // ========== readOnly / writeOnly Tests ==========
    [Fact]
    public void OpenApiParser_ReadOnlyProperty_IsParsed()
    {
        // Verify that Microsoft.OpenApi parses readOnly correctly
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id:
                                      type: integer
                                      readOnly: true
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pet = document.Components.Schemas["Pet"];
        var idProp = pet.Properties["id"];

        // Check if ReadOnly is accessible
        if (idProp is OpenApiSchema directSchema)
        {
            Assert.True(directSchema.ReadOnly, $"Direct OpenApiSchema.ReadOnly should be true (type: {idProp.GetType().Name})");
        }
        else if (idProp is OpenApiSchemaReference schemaRef && schemaRef.Target is OpenApiSchema targetSchema)
        {
            Assert.True(targetSchema.ReadOnly, $"Target OpenApiSchema.ReadOnly should be true");
        }
        else
        {
            Assert.Fail($"Property type is {idProp.GetType().Name}, cannot check ReadOnly");
        }
    }

    [Fact]
    public void ExtractForSchemas_ReadOnlyProperty_IsOptionalEvenIfRequired()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  required:
                                    - id
                                    - name
                                  properties:
                                    id:
                                      type: integer
                                      format: int64
                                      readOnly: true
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.Parameters);

        // 'id' is readOnly + required -> should be treated as optional (nullable)
        var idParam = record.Parameters.First(p => p.Name == "Id");
        Assert.True(idParam.IsNullableType, "readOnly property should be nullable (optional) even when required");

        // 'name' is required and NOT readOnly -> should stay required (non-nullable)
        var nameParam = record.Parameters.First(p => p.Name == "Name");
        Assert.False(nameParam.IsNullableType, "Non-readOnly required property should remain required");
    }

    [Fact]
    public void ExtractForSchemas_WriteOnlyProperty_IsOptionalEvenIfRequired()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                User:
                                  type: object
                                  required:
                                    - email
                                    - password
                                  properties:
                                    email:
                                      type: string
                                    password:
                                      type: string
                                      writeOnly: true
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "User" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.Parameters);

        // 'password' is writeOnly + required -> should be treated as optional (nullable)
        var passwordParam = record.Parameters.First(p => p.Name == "Password");
        Assert.True(passwordParam.IsNullableType, "writeOnly property should be nullable (optional) even when required");

        // 'email' is required and NOT writeOnly -> should stay required
        var emailParam = record.Parameters.First(p => p.Name == "Email");
        Assert.False(emailParam.IsNullableType, "Non-writeOnly required property should remain required");
    }

    // ========== Nullable Property Tests ==========
    [Fact]
    public void ExtractForSchemas_WithNullableProperty_SetsNullableFlag()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                    tag:
                                      type: string
                                      nullable: true
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        // Act
        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        // Assert
        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.Parameters);
        var tagParam = record.Parameters.First(p => p.Name == "Tag");
        Assert.True(tagParam.IsNullableType);
    }

    // ========== Format Mapping Tests ==========
    [Theory]
    [InlineData("integer", "int32", "int")]
    [InlineData("integer", "int64", "long")]
    [InlineData("number", "float", "float")]
    [InlineData("number", "double", "double")]
    [InlineData("string", "uuid", "Guid")]
    [InlineData("string", "date-time", "DateTimeOffset")]
    [InlineData("string", "date", "DateTimeOffset")]
    [InlineData("string", "uri", "Uri")]
    [InlineData("boolean", null, "bool")]
    public void ExtractForSchemas_FormatMapping_ProducesCorrectCSharpType(
        string openApiType,
        string? format,
        string expectedCSharpType)
    {
        var formatLine = format != null ? $"\n          format: {format}" : string.Empty;
        var yaml = "openapi: 3.0.0\n" +
                   "info:\n  title: Test\n  version: 1.0.0\n" +
                   "paths: {}\n" +
                   "components:\n  schemas:\n    TestModel:\n      type: object\n      required:\n        - value\n" +
                   "      properties:\n        value:\n" +
                   $"          type: {openApiType}{formatLine}\n";

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestModel" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var valueParam = result.Parameters[0].Parameters!.First(p => p.Name == "Value");
        Assert.Equal(expectedCSharpType, valueParam.TypeName);
    }

    // ========== allOf Composition Tests ==========
    [Fact]
    public void ExtractForSchemas_AllOfComposition_MergesProperties()
    {
        var yaml = """
                   openapi: 3.0.0
                   info:
                     title: Test
                     version: 1.0.0
                   paths: {}
                   components:
                     schemas:
                       Base:
                         type: object
                         properties:
                           id:
                             type: integer
                             format: int64
                       Extended:
                         allOf:
                           - $ref: '#/components/schemas/Base'
                           - type: object
                             properties:
                               name:
                                 type: string
                   """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Extended" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.Parameters);

        // Should have both id from Base and name from inline
        Assert.Contains(record.Parameters, p => p.Name == "Id");
        Assert.Contains(record.Parameters, p => p.Name == "Name");
    }

    // ========== Required/Optional Ordering Tests ==========
    [Fact]
    public void ExtractForSchemas_RequiredBeforeOptional_CorrectParameterOrder()
    {
        var yaml = """
                   openapi: 3.0.0
                   info:
                     title: Test
                     version: 1.0.0
                   paths: {}
                   components:
                     schemas:
                       Pet:
                         type: object
                         required:
                           - name
                         properties:
                           tag:
                             type: string
                           name:
                             type: string
                           age:
                             type: integer
                             default: 0
                   """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var parameters = result.Parameters[0].Parameters!;

        // Required without defaults first, then optional/defaults last
        // "name" is required (no default) -> first
        // "tag" is optional (no default) -> middle
        // "age" has default -> last
        var nameIndex = parameters.ToList().FindIndex(p => p.Name == "Name");
        var ageIndex = parameters.ToList().FindIndex(p => p.Name == "Age");
        Assert.True(nameIndex < ageIndex, "Required parameter 'Name' should come before defaulted 'Age'");
    }

    // ========== Default Value Tests ==========
    [Fact]
    public void ExtractForSchemas_WithDefaultValue_SetsDefaultOnParameter()
    {
        var yaml = """
                   openapi: 3.0.0
                   info:
                     title: Test
                     version: 1.0.0
                   paths: {}
                   components:
                     schemas:
                       Settings:
                         type: object
                         properties:
                           isActive:
                             type: boolean
                             default: true
                           maxRetries:
                             type: integer
                             default: 3
                   """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Settings" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var parameters = result.Parameters[0].Parameters!;

        var isActiveParam = parameters.First(p => p.Name == "IsActive");
        Assert.Equal("true", isActiveParam.DefaultValue);

        var maxRetriesParam = parameters.First(p => p.Name == "MaxRetries");
        Assert.Equal("3", maxRetriesParam.DefaultValue);
    }

    // ========== Enum Schema Skipping ==========
    [Fact]
    public void ExtractForSchemas_EnumSchema_IsSkipped()
    {
        var yaml = """
                   openapi: 3.0.0
                   info:
                     title: Test
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
                       Pet:
                         type: object
                         properties:
                           name:
                             type: string
                   """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Color", "Pet" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        Assert.Single(result.Parameters);
        Assert.Equal("Pet", result.Parameters[0].Name);
    }

    // ========== Array Property Tests ==========
    [Fact]
    public void ExtractForSchemas_ArrayProperty_ProducesListType()
    {
        var yaml = """
                   openapi: 3.0.0
                   info:
                     title: Test
                     version: 1.0.0
                   paths: {}
                   components:
                     schemas:
                       Order:
                         type: object
                         required:
                           - items
                         properties:
                           items:
                             type: array
                             items:
                               type: string
                   """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Order" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var itemsParam = result.Parameters[0].Parameters!.First(p => p.Name == "Items");
        Assert.Equal("List<string>", itemsParam.TypeName);
    }

    // ========== Schema Description & Example Tests ==========
    [Fact]
    public void ExtractForSchemas_WithSchemaDescription_GeneratesDocumentationTags()
    {
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Pet:
                  type: object
                  description: A pet in the store.
                  properties:
                    name:
                      type: string
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.DocumentationTags);
        Assert.Equal("A pet in the store.", record.DocumentationTags.Summary);
    }

    [Fact]
    public void ExtractForSchemas_WithSchemaExample_GeneratesExampleTag()
    {
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Pet:
                  type: object
                  description: A pet in the store.
                  example:
                    name: Buddy
                    tag: Dog
                  properties:
                    name:
                      type: string
                    tag:
                      type: string
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Pet" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.NotNull(record.DocumentationTags);
        Assert.NotNull(record.DocumentationTags.Example);
        Assert.Contains("Buddy", record.DocumentationTags.Example, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractForSchemas_WithoutDescriptionOrExample_NullDocumentationTags()
    {
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Simple:
                  type: object
                  properties:
                    id:
                      type: string
            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Simple" };

        var result = SchemaExtractor.ExtractForSchemas(
            document,
            "TestProject",
            schemaNames,
            pathSegment: null);

        Assert.NotNull(result);
        var record = result.Parameters[0];
        Assert.Null(record.DocumentationTags);
    }
}