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
            document!,
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
            document!,
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
            document!,
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
            document!,
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

        // ProblemDetails/ValidationProblemDetails should NOT be present
        Assert.DoesNotContain("ProblemDetails", operationFile.ResultClassContent, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidationProblemDetails", operationFile.ResultClassContent, StringComparison.Ordinal);
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
            document!,
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
            document!,
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
            "if (parameters.CorrelationId != null)",
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
            document!,
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
            "if (parameters.Continuation != null)",
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
            document!,
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
            document!,
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
            document!,
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
            "if (parameters.Limit != null)",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);
        Assert.Contains(
            "requestBuilder.WithQueryParameter(\"limit\", parameters.Limit);",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);

        // Required header parameter (no null check)
        Assert.Contains(
            "requestBuilder.WithHeaderParameter(\"x-correlation-id\", parameters.CorrelationId);",
            operationFile.EndpointClassContent,
            StringComparison.Ordinal);

        // Optional header parameter (with null check)
        Assert.Contains(
            "if (parameters.RequestId != null)",
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
            document!,
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

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}