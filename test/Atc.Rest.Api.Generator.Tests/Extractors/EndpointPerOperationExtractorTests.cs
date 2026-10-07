// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class EndpointPerOperationExtractorTests
{
    [Fact]
    public void Extract_ArrayResponse_WithoutAsyncEnumerable_GeneratesIEnumerable()
    {
        // Arrange - simple array response WITHOUT x-return-async-enumerable
        const string yaml = """
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
            document,
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
        const string yaml = """
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
            document,
            "TestApi",
            "devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];

        // For streaming endpoints, result classes are not generated (we use StreamingEndpointResponse directly)
        Assert.Null(operationFile.ResultClassContent);
        Assert.Null(operationFile.ResultInterfaceContent);

        // Endpoint interface should return StreamingEndpointResponse<Device>
        Assert.NotNull(operationFile.EndpointInterfaceContent);
        Assert.Contains("StreamingEndpointResponse<Device>", operationFile.EndpointInterfaceContent, StringComparison.Ordinal);

        // Endpoint class should call BuildStreamingEndpointResponseAsync<Device>
        Assert.NotNull(operationFile.EndpointClassContent);
        Assert.Contains("BuildStreamingEndpointResponseAsync<Device>", operationFile.EndpointClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PaginationAllOf_WithoutAsyncEnumerable_GeneratesPaginatedResult()
    {
        // Arrange - allOf pagination pattern WITHOUT x-return-async-enumerable
        const string yaml = """
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
            document,
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
        const string yaml = """
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
            document,
            "TestApi",
            "devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];

        // For streaming endpoints, result classes are not generated (we use StreamingEndpointResponse directly)
        Assert.Null(operationFile.ResultClassContent);
        Assert.Null(operationFile.ResultInterfaceContent);

        // Endpoint interface should return StreamingEndpointResponse<PaginationResult<Device>>
        Assert.NotNull(operationFile.EndpointInterfaceContent);
        Assert.Contains("StreamingEndpointResponse<PaginationResult<Device>>", operationFile.EndpointInterfaceContent, StringComparison.Ordinal);

        // Endpoint class should call BuildStreamingEndpointResponseAsync
        Assert.NotNull(operationFile.EndpointClassContent);
        Assert.Contains("BuildStreamingEndpointResponseAsync<PaginationResult<Device>>", operationFile.EndpointClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PaginatedResultWithResultsProperty_WithAsyncEnumerable_GeneratesCorrectType()
    {
        // Arrange - using "results" property instead of "items"
        const string yaml = """
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
            document,
            "TestApi",
            "users",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];

        // For streaming endpoints, result classes are not generated (we use StreamingEndpointResponse directly)
        Assert.Null(operationFile.ResultClassContent);
        Assert.Null(operationFile.ResultInterfaceContent);

        // Endpoint interface should return StreamingEndpointResponse<PaginatedResult<User>>
        Assert.NotNull(operationFile.EndpointInterfaceContent);
        Assert.Contains("StreamingEndpointResponse<PaginatedResult<User>>", operationFile.EndpointInterfaceContent, StringComparison.Ordinal);

        // Endpoint class should call BuildStreamingEndpointResponseAsync
        Assert.NotNull(operationFile.EndpointClassContent);
        Assert.Contains("BuildStreamingEndpointResponseAsync<PaginatedResult<User>>", operationFile.EndpointClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ResultInterface_WithAsyncEnumerable_HasCorrectPropertyType()
    {
        // Arrange
        const string yaml = """
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
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];

        // For streaming endpoints, result interfaces/classes are not generated
        Assert.Null(operationFile.ResultInterfaceContent);
        Assert.Null(operationFile.ResultClassContent);

        // Endpoint interface should return StreamingEndpointResponse<Item>
        Assert.NotNull(operationFile.EndpointInterfaceContent);
        Assert.Contains("StreamingEndpointResponse<Item>", operationFile.EndpointInterfaceContent, StringComparison.Ordinal);

        // Endpoint class should call BuildStreamingEndpointResponseAsync<Item>
        Assert.NotNull(operationFile.EndpointClassContent);
        Assert.Contains("BuildStreamingEndpointResponseAsync<Item>", operationFile.EndpointClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_NonSuccessResponse_WithAsyncEnumerable_DoesNotWrapInIAsyncEnumerable()
    {
        // Arrange - error responses should NOT be wrapped in IAsyncEnumerable
        const string yaml = """
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
            document,
            "TestApi",
            "devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];

        // For streaming endpoints, result classes are not generated (we use StreamingEndpointResponse directly)
        // StreamingEndpointResponse<T> handles error responses internally via ErrorContent property
        Assert.Null(operationFile.ResultClassContent);
        Assert.Null(operationFile.ResultInterfaceContent);

        // Endpoint interface should return StreamingEndpointResponse<Device>
        Assert.NotNull(operationFile.EndpointInterfaceContent);
        Assert.Contains("StreamingEndpointResponse<Device>", operationFile.EndpointInterfaceContent, StringComparison.Ordinal);

        // Endpoint class should call BuildStreamingEndpointResponseAsync<Device>
        Assert.NotNull(operationFile.EndpointClassContent);
        Assert.Contains("BuildStreamingEndpointResponseAsync<Device>", operationFile.EndpointClassContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NexusSample.ApiClient", null, "NexusSample-ApiClient")]
    [InlineData("NexusSample.APICLIENT", null, "NexusSample-ApiClient")]
    [InlineData("MyApiClient", null, "My-ApiClient")]
    [InlineData("PetStore.Client", null, "PetStore-Client-ApiClient")]
    [InlineData("MyApi", null, "MyApi-ApiClient")]
    [InlineData("Contoso.Data.Nexus.ApiClient", null, "Contoso-Data-Nexus-ApiClient")]
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

    // ========== Error Response Format Tests ==========
    [Fact]
    public void Extract_ProblemDetailsFormat_Uses_ValidationProblemDetails_For400()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                                    '400':
                                      description: Bad Request
                                    '401':
                                      description: Unauthorized
                                    '500':
                                      description: Internal Server Error
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - default is ProblemDetails format
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false,
            errorResponseFormat: ErrorResponseFormatType.ProblemDetails);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.ResultClassContent);

        // 400 should use ValidationProblemDetails
        Assert.Contains("ValidationProblemDetails BadRequestContent", operationFile.ResultClassContent, StringComparison.Ordinal);

        // Other errors should use ProblemDetails
        Assert.Contains("ProblemDetails UnauthorizedContent", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.Contains("ProblemDetails InternalServerErrorContent", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ProblemDetailsFormat_HasNullContentObjectFallback()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                                    '400':
                                      description: Bad Request
                                    '401':
                                      description: Unauthorized
                                    '500':
                                      description: Internal Server Error
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false,
            errorResponseFormat: ErrorResponseFormatType.ProblemDetails);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.ResultClassContent);

        // 401 (ProblemDetails) should have string message fallback
        Assert.Contains("IsUnauthorized && ContentObject is string message", operationFile.ResultClassContent, StringComparison.Ordinal);

        // 401 (ProblemDetails) should have null ContentObject fallback
        Assert.Contains("IsUnauthorized && ContentObject is null", operationFile.ResultClassContent, StringComparison.Ordinal);

        // 500 (ProblemDetails) should have string message fallback
        Assert.Contains("IsInternalServerError && ContentObject is string message", operationFile.ResultClassContent, StringComparison.Ordinal);

        // 500 (ProblemDetails) should have null ContentObject fallback
        Assert.Contains("IsInternalServerError && ContentObject is null", operationFile.ResultClassContent, StringComparison.Ordinal);

        // 400 uses ValidationProblemDetails and has the same fallbacks, through CreateValidation
        Assert.Contains("IsBadRequest && ContentObject is string message", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.Contains("IsBadRequest && ContentObject is null", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.Contains("ProblemDetailsFactory.CreateValidation(HttpStatusCode.BadRequest, message)", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PlainTextFormat_Uses_String_ForNon400Errors()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                                    '400':
                                      description: Bad Request
                                    '401':
                                      description: Unauthorized
                                    '500':
                                      description: Internal Server Error
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - PlainText format
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false,
            errorResponseFormat: ErrorResponseFormatType.PlainText);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.ResultClassContent);

        // 400 should still use ValidationProblemDetails
        Assert.Contains("ValidationProblemDetails BadRequestContent", operationFile.ResultClassContent, StringComparison.Ordinal);

        // Other errors should use string
        Assert.Contains("string UnauthorizedContent", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.Contains("string InternalServerErrorContent", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PlainTextOnlyFormat_Uses_String_ForAllErrors()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                                    '400':
                                      description: Bad Request
                                    '401':
                                      description: Unauthorized
                                    '500':
                                      description: Internal Server Error
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - PlainTextOnly format
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false,
            errorResponseFormat: ErrorResponseFormatType.PlainTextOnly);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.ResultClassContent);

        // All errors should use string including 400
        Assert.Contains("string BadRequestContent", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.Contains("string UnauthorizedContent", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.Contains("string InternalServerErrorContent", operationFile.ResultClassContent, StringComparison.Ordinal);

        // ValidationProblemDetails should NOT be present
        Assert.DoesNotContain("ValidationProblemDetails", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_CustomFormat_Uses_CustomTypeName_ForAllErrors()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                                    '400':
                                      description: Bad Request
                                    '401':
                                      description: Unauthorized
                                    '500':
                                      description: Internal Server Error
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - Custom format with custom error type name
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false,
            errorResponseFormat: ErrorResponseFormatType.Custom,
            customErrorTypeName: "ApiError");

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.ResultClassContent);

        // All errors should use ApiError
        Assert.Contains("ApiError BadRequestContent", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.Contains("ApiError UnauthorizedContent", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.Contains("ApiError InternalServerErrorContent", operationFile.ResultClassContent, StringComparison.Ordinal);

        // ProblemDetails/ValidationProblemDetails types should NOT be present (the lenient reader of the custom
        // model lives on ProblemDetailsFactory, so that name is allowed)
        var withoutFactory = operationFile.ResultClassContent!.Replace("ProblemDetailsFactory.", string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("ProblemDetails", withoutFactory, StringComparison.Ordinal);
        Assert.Contains("ProblemDetailsFactory.ModelFromContent<ApiError>(Content)", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_StringError_ReadsAProblemJsonBodyAsProblemDetails_AndGivesItsMessage()
    {
        // Arrange - a declared string (text/plain) error, and PlainText for the undeclared ones
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  responses:
                                    '204':
                                      description: No Content
                                    '404':
                                      description: Not Found
                                      content:
                                        text/plain:
                                          schema:
                                            type: string
                                    '409':
                                      description: Conflict
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false,
            errorResponseFormat: ErrorResponseFormatType.PlainText);

        // Assert - the host wiring's ProblemDetails (a handler that sent no body) is split off by media type ...
        var operationFile = Assert.Single(files);
        var endpoint = operationFile.EndpointClassContent!;
        Assert.Contains("var isProblemJson = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() is \"application/problem+json\";", endpoint, StringComparison.Ordinal);
        Assert.Contains("responseBuilder.AddErrorResponse<ProblemDetails>(HttpStatusCode.NotFound);", endpoint, StringComparison.Ordinal);
        Assert.Contains("responseBuilder.AddErrorTextResponse(HttpStatusCode.NotFound);", endpoint, StringComparison.Ordinal);
        Assert.Contains("responseBuilder.AddErrorResponse<ProblemDetails>(HttpStatusCode.Conflict);", endpoint, StringComparison.Ordinal);
        Assert.Contains("responseBuilder.AddErrorResponse<string>(HttpStatusCode.Conflict);", endpoint, StringComparison.Ordinal);

        // ... and the string accessor gives its message (detail, else title), or the raw body - it never throws.
        var result = operationFile.ResultClassContent!;
        Assert.Contains("? string.IsNullOrEmpty(problem.Detail) ? problem.Title ?? string.Empty : problem.Detail", result, StringComparison.Ordinal);
        Assert.Contains(": IsNotFound\n                    ? Content".Replace("\n", Environment.NewLine, StringComparison.Ordinal), result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_CustomFormat_WithoutCustomTypeName_FallsBackToProblemDetails()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                                    '401':
                                      description: Unauthorized
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - Custom format but no custom type name (fallback)
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false,
            errorResponseFormat: ErrorResponseFormatType.Custom,
            customErrorTypeName: null);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.ResultClassContent);

        // Should fall back to ProblemDetails
        Assert.Contains("ProblemDetails UnauthorizedContent", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    // ========== Header Parameter Tests ==========
    [Fact]
    public void Extract_RequiredHeaderParameter_GeneratesWithHeaderParameterWithoutNullCheck()
    {
        // Arrange - required header parameter
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  parameters:
                                    - name: x-correlation-id
                                      in: header
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.EndpointClassContent);

        // Required header should have WithHeaderParameter call without null check
        Assert.Contains(
            "requestBuilder.WithHeaderParameter(\"x-correlation-id\", parameters.CorrelationId);",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);

        // Should NOT have null check for required parameter
        Assert.DoesNotContain(
            "if (parameters.CorrelationId is not null)",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_OptionalHeaderParameter_GeneratesWithHeaderParameterWithNullCheck()
    {
        // Arrange - optional header parameter
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  parameters:
                                    - name: x-continuation
                                      in: header
                                      required: false
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.EndpointClassContent);

        // Optional header should have null check
        Assert.Contains(
            "if (parameters.Continuation is not null)",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);

        // And the WithHeaderParameter call inside the condition
        Assert.Contains(
            "requestBuilder.WithHeaderParameter(\"x-continuation\", parameters.Continuation);",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_HeaderParameterWithXPrefix_StripsXPrefixForPropertyName()
    {
        // Arrange - header with x- prefix
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  parameters:
                                    - name: x-request-id
                                      in: header
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.EndpointClassContent);

        // Property name should be "RequestId" (x- prefix stripped and PascalCased)
        // But header name in the call should remain "x-request-id"
        Assert.Contains(
            "requestBuilder.WithHeaderParameter(\"x-request-id\", parameters.RequestId);",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_HeaderParameterWithoutXPrefix_UsesFullNameForPropertyName()
    {
        // Arrange - header without x- prefix (like api_key)
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  parameters:
                                    - name: api_key
                                      in: header
                                      required: false
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.EndpointClassContent);

        // Property name should be "ApiKey" (PascalCased from api_key, no x- prefix to strip)
        Assert.Contains(
            "requestBuilder.WithHeaderParameter(\"api_key\", parameters.ApiKey);",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_BooleanQueryAndHeaderParameters_AreSentLowercase()
    {
        // Arrange - bool.ToString() writes True/False; the wire writes true/false
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  parameters:
                                    - name: printable
                                      in: query
                                      required: false
                                      schema:
                                        type: boolean
                                    - name: isActive
                                      in: query
                                      required: true
                                      schema:
                                        type: boolean
                                    - name: x-dry-run
                                      in: header
                                      required: false
                                      schema:
                                        type: boolean
                                  responses:
                                    '204':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false);

        // Assert
        var content = Assert.Single(files).EndpointClassContent;
        Assert.NotNull(content);
        Assert.Contains(
            "requestBuilder.WithQueryParameter(\"printable\", ((bool?)parameters.Printable) switch { true => \"true\", false => \"false\", _ => null });",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "requestBuilder.WithQueryParameter(\"isActive\", ((bool?)parameters.IsActive) switch { true => \"true\", false => \"false\", _ => null });",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "((bool?)parameters.DryRun) switch { true => \"true\", false => \"false\", _ => null }",
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_MixedParameters_GeneratesAllParameterTypes()
    {
        // Arrange - path, query, and header parameters combined
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items/{itemId}:
                                get:
                                  operationId: getItemById
                                  parameters:
                                    - name: itemId
                                      in: path
                                      required: true
                                      schema:
                                        type: string
                                    - name: limit
                                      in: query
                                      required: false
                                      schema:
                                        type: integer
                                    - name: x-correlation-id
                                      in: header
                                      required: true
                                      schema:
                                        type: string
                                    - name: x-request-id
                                      in: header
                                      required: false
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: object
                                            properties:
                                              id:
                                                type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.EndpointClassContent);

        // Path parameter
        Assert.Contains(
            "requestBuilder.WithPathParameter(\"itemId\", parameters.ItemId);",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);

        // Query parameter with null check (optional)
        Assert.Contains(
            "if (parameters.Limit is not null)",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);
        Assert.Contains(
            "requestBuilder.WithQueryParameter(\"limit\", ((global::System.IFormattable?)parameters.Limit)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture));",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);

        // Required header parameter (no null check)
        Assert.Contains(
            "requestBuilder.WithHeaderParameter(\"x-correlation-id\", parameters.CorrelationId);",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);

        // Optional header parameter (with null check)
        Assert.Contains(
            "if (parameters.RequestId is not null)",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);
        Assert.Contains(
            "requestBuilder.WithHeaderParameter(\"x-request-id\", parameters.RequestId);",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_HeaderParameterReference_UsesReferenceIdForPropertyName()
    {
        // Arrange - header parameter defined as a $ref in components/parameters
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: getItems
                                  parameters:
                                    - $ref: '#/components/parameters/continuationToken'
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                            components:
                              parameters:
                                continuationToken:
                                  name: x-continuation
                                  in: header
                                  required: false
                                  schema:
                                    type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "items",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.Single(files);
        var operationFile = files[0];
        Assert.NotNull(operationFile.EndpointClassContent);

        // Property name should be "ContinuationToken" (from reference ID, PascalCased)
        // Header name in the call should still be "x-continuation"
        Assert.Contains(
            "requestBuilder.WithHeaderParameter(\"x-continuation\", parameters.ContinuationToken);",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ItemSchemaStreamingResponse_GeneratesStreamingEndpointResponse()
    {
        // Arrange - OpenAPI 3.2 itemSchema streaming, no x-return-async-enumerable.
        const string yaml = """
                            openapi: 3.2.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /events:
                                get:
                                  operationId: streamEvents
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/jsonl:
                                          itemSchema:
                                            $ref: '#/components/schemas/Event'
                            components:
                              schemas:
                                Event:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(
            document,
            "TestApi",
            "events",
            registry: null,
            includeDeprecated: false);

        // Assert
        var operationFile = Assert.Single(files);
        Assert.NotNull(operationFile.EndpointInterfaceContent);
        Assert.Contains("StreamingEndpointResponse<Event>", operationFile.EndpointInterfaceContent, StringComparison.Ordinal);
        Assert.NotNull(operationFile.EndpointClassContent);

        // application/jsonl is real JSON Lines wire-framing, so the per-op endpoint reads it via the
        // emitted StreamReaders.ReadJsonLinesAsync (using the DI-configured IContractSerializer) and
        // builds the StreamingEndpointResponse<T> inline — it does NOT use the JSON-array-based
        // BuildStreamingEndpointResponseAsync reader.
        Assert.Contains("ReadJsonLinesAsync<Event>", operationFile.EndpointClassContent, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildStreamingEndpointResponseAsync<Event>", operationFile.EndpointClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ResultClass_IsPartialAndNotSealed()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /devices/{id}:
                                get:
                                  operationId: getDeviceById
                                  parameters:
                                    - name: id
                                      in: path
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Device'
                                    '404':
                                      description: Not Found
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
            document,
            "TestApi",
            "devices",
            registry: null,
            includeDeprecated: false);

        // Assert - the result class must be extensible via a partial declaration so consumers
        // can add their own members, and must not be sealed.
        var operationFile = Assert.Single(files);
        Assert.NotNull(operationFile.ResultClassContent);
        Assert.Contains("public partial class GetDeviceByIdEndpointResult", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.DoesNotContain("sealed class GetDeviceByIdEndpointResult", operationFile.ResultClassContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_RawFileBodyOfOneMediaType_IsSentAsThatMediaType()
    {
        // The operation accepts application/octet-stream only: a text file sent as text/plain was a 415
        var document = ParseYaml(RawFileBodySpecs.Yaml("application/octet-stream"));
        Assert.NotNull(document);

        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(document, "TestApi", "upload", registry: null, includeDeprecated: false);

        var content = Assert.Single(files).EndpointClassContent!;
        Assert.Contains("requestBuilder.WithBinaryBody(parameters.File!.OpenReadStream(), \"application/octet-stream\");", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_RawFileBodyOfSeveralMediaTypes_IsSentAsTheFilesMediaType()
    {
        // A PDF or a PNG: the file's own media type says which
        var document = ParseYaml(RawFileBodySpecs.Yaml("application/pdf", "image/png"));
        Assert.NotNull(document);

        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(document, "TestApi", "upload", registry: null, includeDeprecated: false);

        var content = Assert.Single(files).EndpointClassContent!;
        Assert.Contains("requestBuilder.WithBinaryBody(parameters.File!.OpenReadStream(), parameters.File.ContentType ?? \"application/pdf\");", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_RawFileBodyOfAMediaTypeRange_IsSentAsTheFilesMediaType()
    {
        var document = ParseYaml(RawFileBodySpecs.Yaml("image/*"));
        Assert.NotNull(document);

        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(document, "TestApi", "upload", registry: null, includeDeprecated: false);

        var content = Assert.Single(files).EndpointClassContent!;
        Assert.Contains("requestBuilder.WithBinaryBody(parameters.File!.OpenReadStream(), parameters.File.ContentType ?? \"image/*\");", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_UrlEncodedFormBody_SendsTheFieldsAsAFormUrlEncodedBody()
    {
        // The endpoint called WithBody(parameters.Request) on a record without a Request: the client did not compile.
        // The request builder has no URL-encoded form of its own, so the fields are encoded by FormUrlEncodedContent
        // and sent as a binary body of the form's media type.
        var document = ParseYaml(UrlEncodedFormYaml);
        Assert.NotNull(document);

        var (files, _) = EndpointPerOperationExtractor.ExtractWithInlineSchemas(document, "TestApi", "login", registry: null, includeDeprecated: false);

        var content = Assert.Single(files).EndpointClassContent!;
        Assert.Contains("var fields = new List<KeyValuePair<string, string>>();", content, StringComparison.Ordinal);
        Assert.Contains("fields.Add(new KeyValuePair<string, string>(\"user\", user));", content, StringComparison.Ordinal);
        Assert.Contains("fields.Add(new KeyValuePair<string, string>(\"role\", role.ToWireValue()));", content, StringComparison.Ordinal);
        Assert.Contains("using var formContent = new FormUrlEncodedContent(fields);", content, StringComparison.Ordinal);
        Assert.Contains("requestBuilder.WithBinaryBody(formContent.ReadAsStream(), \"application/x-www-form-urlencoded\");", content, StringComparison.Ordinal);
        Assert.DoesNotContain("WithBody(parameters.Request)", content, StringComparison.Ordinal);
    }

    private const string UrlEncodedFormYaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        paths:
          /login:
            post:
              operationId: login
              requestBody:
                required: true
                content:
                  application/x-www-form-urlencoded:
                    schema: { $ref: '#/components/schemas/Credentials' }
              responses:
                '204': { description: ok }
        components:
          schemas:
            Role:
              type: string
              enum: [admin, read-only]
            Credentials:
              type: object
              required: [user]
              properties:
                user: { type: string }
                remember: { type: boolean }
                role: { $ref: '#/components/schemas/Role' }
                scopes: { type: array, items: { type: string } }
        """;

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}