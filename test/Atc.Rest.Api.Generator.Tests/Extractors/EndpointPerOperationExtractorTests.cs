// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class EndpointPerOperationExtractorTests
{
    [Fact]
    public void Extract_ArrayResponse_WithoutAsyncEnumerable_GeneratesIEnumerable()
    {
        // Arrange - simple array response WITHOUT x-return-async-enumerable
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /devices:
                get:
                  operationId: getDevices
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Device'
            components:
              schemas:
                Device:
                  type: object
                  properties:
                    id:
                      type: string
                    name:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document!,
            "TestApi",
            "devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];

        // Result class should use IEnumerable<Device>, not IAsyncEnumerable
        Assert.NotNull(operationFile.ResultClassContent);
        Assert.Contains("IEnumerable<Device>", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.DoesNotContain("IAsyncEnumerable", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ArrayResponse_WithAsyncEnumerable_GeneratesIAsyncEnumerable()
    {
        // Arrange - simple array response WITH x-return-async-enumerable: true
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /devices:
                get:
                  operationId: getDevices
                  x-return-async-enumerable: true
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Device'
            components:
              schemas:
                Device:
                  type: object
                  properties:
                    id:
                      type: string
                    name:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document!,
            "TestApi",
            "devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];

        // Result class should use IAsyncEnumerable<Device>
        Assert.NotNull(operationFile.ResultClassContent);
        Assert.Contains("IAsyncEnumerable<Device>", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PaginationAllOf_WithoutAsyncEnumerable_GeneratesPaginatedResult()
    {
        // Arrange - allOf pagination pattern WITHOUT x-return-async-enumerable
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /devices:
                get:
                  operationId: getDevices
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            allOf:
                              - $ref: '#/components/schemas/PaginationResult'
                              - type: object
                                properties:
                                  items:
                                    type: array
                                    items:
                                      $ref: '#/components/schemas/Device'
            components:
              schemas:
                PaginationResult:
                  type: object
                  properties:
                    pageSize:
                      type: integer
                    continuationToken:
                      type: string
                Device:
                  type: object
                  properties:
                    id:
                      type: string
                    name:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document!,
            "TestApi",
            "devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];

        // Result class should use PaginationResult<Device>, not wrapped in IAsyncEnumerable
        Assert.NotNull(operationFile.ResultClassContent);
        Assert.Contains("PaginationResult<Device>", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.DoesNotContain("IAsyncEnumerable", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PaginationAllOf_WithAsyncEnumerable_GeneratesIAsyncEnumerablePaginatedResult()
    {
        // Arrange - allOf pagination pattern WITH x-return-async-enumerable: true
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /devices:
                get:
                  operationId: getDevices
                  x-return-async-enumerable: true
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            allOf:
                              - $ref: '#/components/schemas/PaginationResult'
                              - type: object
                                properties:
                                  items:
                                    type: array
                                    items:
                                      $ref: '#/components/schemas/Device'
            components:
              schemas:
                PaginationResult:
                  type: object
                  properties:
                    pageSize:
                      type: integer
                    continuationToken:
                      type: string
                Device:
                  type: object
                  properties:
                    id:
                      type: string
                    name:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document!,
            "TestApi",
            "devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];

        // Result class should use IAsyncEnumerable<PaginationResult<Device>>
        Assert.NotNull(operationFile.ResultClassContent);
        Assert.Contains("IAsyncEnumerable<PaginationResult<Device>>", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PaginatedResultWithResultsProperty_WithAsyncEnumerable_GeneratesCorrectType()
    {
        // Arrange - using "results" property instead of "items"
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /users:
                get:
                  operationId: getUsers
                  x-return-async-enumerable: true
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            allOf:
                              - $ref: '#/components/schemas/PaginatedResult'
                              - type: object
                                properties:
                                  results:
                                    type: array
                                    items:
                                      $ref: '#/components/schemas/User'
            components:
              schemas:
                PaginatedResult:
                  type: object
                  properties:
                    total:
                      type: integer
                    page:
                      type: integer
                User:
                  type: object
                  properties:
                    id:
                      type: string
                    email:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document!,
            "TestApi",
            "users",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];

        // Result class should use IAsyncEnumerable<PaginatedResult<User>>
        Assert.NotNull(operationFile.ResultClassContent);
        Assert.Contains("IAsyncEnumerable<PaginatedResult<User>>", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ResultInterface_WithAsyncEnumerable_HasCorrectPropertyType()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /items:
                get:
                  operationId: getItems
                  x-return-async-enumerable: true
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Item'
            components:
              schemas:
                Item:
                  type: object
                  properties:
                    id:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document!,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];

        // Both interface and class should have IAsyncEnumerable<Item> OkContent
        Assert.NotNull(operationFile.ResultInterfaceContent);
        Assert.NotNull(operationFile.ResultClassContent);

        Assert.Contains("IAsyncEnumerable<Item> OkContent", operationFile.ResultInterfaceContent, StringComparison.Ordinal);
        Assert.Contains("IAsyncEnumerable<Item> OkContent", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_NonSuccessResponse_WithAsyncEnumerable_DoesNotWrapInIAsyncEnumerable()
    {
        // Arrange - error responses should NOT be wrapped in IAsyncEnumerable
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /devices:
                get:
                  operationId: getDevices
                  x-return-async-enumerable: true
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Device'
                    '400':
                      description: Bad Request
            components:
              schemas:
                Device:
                  type: object
                  properties:
                    id:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document!,
            "TestApi",
            "devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.ResultClassContent);

        // 200 OK should be IAsyncEnumerable<Device>
        Assert.Contains("IAsyncEnumerable<Device> OkContent", operationFile.ResultClassContent, StringComparison.Ordinal);

        // 400 BadRequest should be ValidationProblemDetails, NOT wrapped
        Assert.Contains("ValidationProblemDetails BadRequestContent", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.DoesNotContain("IAsyncEnumerable<ValidationProblemDetails>", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NexusSample.ApiClient", null, "NexusSample-ApiClient")]
    [InlineData("NexusSample.APICLIENT", null, "NexusSample-ApiClient")]
    [InlineData("MyApiClient", null, "My-ApiClient")]
    [InlineData("PetStore.Client", null, "PetStore-Client-ApiClient")]
    [InlineData("MyApi", null, "MyApi-ApiClient")]
    [InlineData("Contoso.IoT.Nexus.ApiClient", null, "Contoso-IoT-Nexus-ApiClient")]
    public void GetEffectiveHttpClientName_NullConfig_ReturnsExpectedName(
        string projectName,
        string? configuredName,
        string expected)
    {
        // Act
        var result = EndpointPerOperationExtractor.GetEffectiveHttpClientName(projectName, configuredName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("NexusSample.ApiClient", "Custom", "NexusSample-Custom")]
    [InlineData("MyApiClient", "MyService", "My-MyService")]
    [InlineData("PetStore", "PetService", "PetStore-PetService")]
    public void GetEffectiveHttpClientName_SimpleName_CombinesWithBase(
        string projectName,
        string configuredName,
        string expected)
    {
        // Act
        var result = EndpointPerOperationExtractor.GetEffectiveHttpClientName(projectName, configuredName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("NexusSample.ApiClient", "My.Custom.Client", "My.Custom.Client")]
    [InlineData("NexusSample.ApiClient", "My-Custom-Client", "My-Custom-Client")]
    [InlineData("PetStore", "Custom.Service-Name", "Custom.Service-Name")]
    public void GetEffectiveHttpClientName_FullName_UsesAsIs(
        string projectName,
        string configuredName,
        string expected)
    {
        // Act
        var result = EndpointPerOperationExtractor.GetEffectiveHttpClientName(projectName, configuredName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("NexusSample.ApiClient", "", "NexusSample-ApiClient")]
    [InlineData("NexusSample.ApiClient", "   ", "NexusSample-ApiClient")]
    public void GetEffectiveHttpClientName_EmptyOrWhitespaceConfig_TreatedAsNull(
        string projectName,
        string configuredName,
        string expected)
    {
        // Act
        var result = EndpointPerOperationExtractor.GetEffectiveHttpClientName(projectName, configuredName);

        // Assert
        Assert.Equal(expected, result);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}