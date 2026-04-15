namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptModelExtractorTests
{
    [Fact]
    public void Extract_PaginatedResultWithEmptyItems_GeneratesGenericInterface()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths: {}
            components:
              schemas:
                PaginatedResult:
                  type: object
                  properties:
                    pageSize:
                      type: integer
                    results:
                      type: array
                      items: {}
            """);

        var config = new TypeScriptClientConfig();

        // Act
        var results = TypeScriptModelExtractor.Extract(document, config);

        // Assert
        Assert.Single(results);
        var (name, parameters) = results[0];
        Assert.Equal("PaginatedResult", name);
        Assert.Equal("PaginatedResult<T>", parameters.TypeName);

        // The results property should be T[]
        var resultsProp = parameters.Properties?.FirstOrDefault(p => p.Name == "results");
        Assert.NotNull(resultsProp);
        Assert.Equal("T[]", resultsProp!.TypeAnnotation);
    }

    [Fact]
    public void Extract_RegularArrayProperty_DoesNotGenerateGeneric()
    {
        // Arrange
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths: {}
            components:
              schemas:
                UserList:
                  type: object
                  properties:
                    users:
                      type: array
                      items:
                        type: string
            """);

        var config = new TypeScriptClientConfig();

        // Act
        var results = TypeScriptModelExtractor.Extract(document, config);

        // Assert
        Assert.Single(results);
        var (_, parameters) = results[0];
        Assert.Equal("UserList", parameters.TypeName); // No <T>

        var usersProp = parameters.Properties?.FirstOrDefault(p => p.Name == "users");
        Assert.NotNull(usersProp);
        Assert.Equal("string[]", usersProp!.TypeAnnotation);
    }

    [Fact]
    public void Extract_PagedResultWithNullItems_GeneratesGenericInterface()
    {
        // Arrange — different pagination type name
        var document = OpenApiDocumentHelper.ParseYaml("""
            openapi: 3.0.0
            info:
              title: Test
              version: 1.0.0
            paths: {}
            components:
              schemas:
                PagedResult:
                  type: object
                  properties:
                    totalCount:
                      type: integer
                    items:
                      type: array
                      items: {}
            """);

        var config = new TypeScriptClientConfig();

        // Act
        var results = TypeScriptModelExtractor.Extract(document, config);

        // Assert
        Assert.Single(results);
        Assert.Equal("PagedResult<T>", results[0].Parameters.TypeName);
    }
}