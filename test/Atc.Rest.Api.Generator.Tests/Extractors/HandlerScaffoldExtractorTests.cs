namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class HandlerScaffoldExtractorTests
{
    [Fact]
    public void Extract_InjectLoggerFalse_NoConstructorOrField()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets/{petId}:
                                get:
                                  operationId: getPetById
                                  parameters:
                                    - name: petId
                                      in: path
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pathItem = document.Paths["/pets/{petId}"];
        var operation = pathItem.Operations.Values.First();
        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = HandlerScaffoldExtractor.Extract(
            "GetPetByIdHandler",
            "TestApi.Handlers.Pets",
            operation,
            (OpenApiPathItem)pathItem,
            "getPetById",
            "Handler",
            "throw-not-implemented",
            resolver,
            injectLogger: false);

        // Assert
        Assert.Null(result.Constructors);
        Assert.Null(result.HeaderContent);
    }

    [Fact]
    public void Extract_InjectLoggerTrue_GeneratesConstructorWithLogger()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /pets/{petId}:
                                get:
                                  operationId: getPetById
                                  parameters:
                                    - name: petId
                                      in: path
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pathItem = document.Paths["/pets/{petId}"];
        var operation = pathItem.Operations.Values.First();
        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = HandlerScaffoldExtractor.Extract(
            "GetPetByIdHandler",
            "TestApi.Handlers.Pets",
            operation,
            (OpenApiPathItem)pathItem,
            "getPetById",
            "Handler",
            "throw-not-implemented",
            resolver,
            injectLogger: true);

        // Assert
        Assert.NotNull(result.Constructors);
        Assert.Single(result.Constructors);

        var constructor = result.Constructors[0];
        Assert.NotNull(constructor.Parameters);
        Assert.Single(constructor.Parameters);
        Assert.Equal("ILogger<GetPetByIdHandler>", constructor.Parameters[0].TypeName);
        Assert.Equal("logger", constructor.Parameters[0].Name);
        Assert.True(constructor.Parameters[0].CreateAsPrivateReadonlyMember);
    }

    [Fact]
    public void Extract_InjectLoggerTrue_NoHeaderContent_UsingsInGlobalUsings()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /health:
                                get:
                                  operationId: getHealth
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pathItem = document.Paths["/health"];
        var operation = pathItem.Operations.Values.First();
        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = HandlerScaffoldExtractor.Extract(
            "GetHealthHandler",
            "TestApi.Handlers",
            operation,
            (OpenApiPathItem)pathItem,
            "getHealth",
            "Handler",
            "throw-not-implemented",
            resolver,
            injectLogger: true);

        // Assert — using goes in GlobalUsings.cs, not per-file HeaderContent
        Assert.Null(result.HeaderContent);
        Assert.NotNull(result.Constructors);
    }

    [Fact]
    public void Extract_DefaultParameter_IsFalse()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /health:
                                get:
                                  operationId: getHealth
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pathItem = document.Paths["/health"];
        var operation = pathItem.Operations.Values.First();
        var resolver = new SystemTypeConflictResolver([]);

        // Act — call without injectLogger parameter (defaults to false)
        var result = HandlerScaffoldExtractor.Extract(
            "GetHealthHandler",
            "TestApi.Handlers",
            operation,
            (OpenApiPathItem)pathItem,
            "getHealth",
            "Handler",
            "throw-not-implemented",
            resolver);

        // Assert
        Assert.Null(result.Constructors);
        Assert.Null(result.HeaderContent);
    }

    [Fact]
    public void Extract_CancellationTokenOnly_BreaksDownBecauseExceeds80()
    {
        // Arrange — even with short name "ping", CancellationToken param exceeds 80 chars:
        // "    public Task<PingResult> ExecuteAsync(CancellationToken cancellationToken = default)" = 87 chars
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /ping:
                                get:
                                  operationId: ping
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pathItem = document.Paths["/ping"];
        var operation = pathItem.Operations.Values.First();
        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = HandlerScaffoldExtractor.Extract(
            "PingHandler",
            "TestApi.Handlers",
            operation,
            (OpenApiPathItem)pathItem,
            "ping",
            "Handler",
            "throw-not-implemented",
            resolver);

        // Assert — CancellationToken + default is always > 80 chars, so always breaks
        Assert.NotNull(result.Methods);
        Assert.True(result.Methods[0].AlwaysBreakDownParameters);
    }

    [Fact]
    public void Extract_ExceedsDefault80ButFitsUnder120_KeepsOnSameLineWith120()
    {
        // Arrange — "getHealth" signature is ~92 chars, exceeds 80 but fits under 120
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /health:
                                get:
                                  operationId: getHealth
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pathItem = document.Paths["/health"];
        var operation = pathItem.Operations.Values.First();
        var resolver = new SystemTypeConflictResolver([]);

        // Act — with maxLineLength=120
        var result = HandlerScaffoldExtractor.Extract(
            "GetHealthHandler",
            "TestApi.Handlers",
            operation,
            (OpenApiPathItem)pathItem,
            "getHealth",
            "Handler",
            "throw-not-implemented",
            resolver,
            maxLineLength: 120);

        // Assert — fits under 120 so keeps on same line
        Assert.NotNull(result.Methods);
        Assert.False(result.Methods[0].AlwaysBreakDownParameters);
    }

    [Fact]
    public void Extract_LongSignature_BreaksDownParams()
    {
        // Arrange — long operation name produces a signature > 80 chars
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /very-long-resource-name/{id}:
                                get:
                                  operationId: getVeryLongResourceNameByIdentifier
                                  parameters:
                                    - name: id
                                      in: path
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var pathItem = document.Paths["/very-long-resource-name/{id}"];
        var operation = pathItem.Operations.Values.First();
        var resolver = new SystemTypeConflictResolver([]);

        // Act
        var result = HandlerScaffoldExtractor.Extract(
            "GetVeryLongResourceNameByIdentifierHandler",
            "TestApi.Handlers",
            operation,
            (OpenApiPathItem)pathItem,
            "getVeryLongResourceNameByIdentifier",
            "Handler",
            "throw-not-implemented",
            resolver);

        // Assert — long signature should break down (has both params + CancellationToken = Count > 1)
        Assert.NotNull(result.Methods);
        Assert.True(result.Methods[0].AlwaysBreakDownParameters);
    }
}