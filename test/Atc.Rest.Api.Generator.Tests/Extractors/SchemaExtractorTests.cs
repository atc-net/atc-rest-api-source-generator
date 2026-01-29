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
}