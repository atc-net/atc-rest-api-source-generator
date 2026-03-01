namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class HandlerExtractorTests
{
    [Fact]
    public void Extract_SimpleGetEndpoint_ReturnsHandlerInterface()
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
                                  summary: List all pets
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = HandlerExtractor.Extract(document!, "TestProject", resolver);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        var handler = result[0];
        Assert.Equal("IListPetsHandler", handler.InterfaceTypeName);
        Assert.NotNull(handler.Methods);
        Assert.Single(handler.Methods);
        Assert.Equal("ExecuteAsync", handler.Methods[0].Name);
    }

    [Fact]
    public void Extract_WithRequestBody_IncludesParameterType()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                post:
                                  operationId: createPet
                                  summary: Create a pet
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/CreatePetRequest'
                                  responses:
                                    '201':
                                      description: Created
                            components:
                              schemas:
                                CreatePetRequest:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = HandlerExtractor.Extract(document!, "TestProject", resolver);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        var handler = result[0];
        var method = handler.Methods![0];

        // Should have a parameters parameter (for request body) plus CancellationToken
        Assert.Equal(2, method.Parameters!.Count);
        Assert.Equal("CreatePetParameters", method.Parameters[0].TypeName);
        Assert.Equal("CancellationToken", method.Parameters[1].TypeName);
    }

    [Fact]
    public void Extract_DeprecatedOperation_SkippedByDefault()
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
                                  deprecated: true
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = HandlerExtractor.Extract(document!, "TestProject", resolver);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Extract_DeprecatedOperation_IncludedWhenFlagSet()
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
                                  deprecated: true
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = HandlerExtractor.Extract(document!, "TestProject", resolver, includeDeprecated: true);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public void Extract_NullDocument_ThrowsArgumentNullException()
    {
        // Arrange
        var resolver = new SystemTypeConflictResolver([]);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            HandlerExtractor.Extract(null!, "TestProject", resolver));
    }

    [Fact]
    public void Extract_NoPaths_ReturnsNull()
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

        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = HandlerExtractor.Extract(document!, "TestProject", resolver);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Extract_WithPathSegment_FiltersOperations()
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
                              /users:
                                get:
                                  operationId: listUsers
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var resolver = new SystemTypeConflictResolver([]);

        // Act — filter to only "Pets" segment
        var result = HandlerExtractor.Extract(document!, "TestProject", "Pets", resolver);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("IListPetsHandler", result[0].InterfaceTypeName);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}