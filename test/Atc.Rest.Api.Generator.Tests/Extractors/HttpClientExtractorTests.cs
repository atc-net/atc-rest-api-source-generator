namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class HttpClientExtractorTests
{
    // ========== IsPaginationBaseType Tests ==========
    [Theory]
    [InlineData("PaginationResult", true)]
    [InlineData("PaginationResultOfT", true)]
    [InlineData("PaginatedResult", true)]
    [InlineData("PaginatedResultOfItems", true)]
    [InlineData("PagedResult", true)]
    [InlineData("PagedResultBase", true)]
    [InlineData("paginationResult", false)] // case-sensitive - must start with uppercase
    [InlineData("MyPaginationResult", false)] // must start with pagination type name
    [InlineData("Pagination", false)] // must be full name
    [InlineData("Result", false)]
    [InlineData("Page", false)]
    [InlineData("", false)]
    public void IsPaginationBaseType_ReturnsExpectedResult(
        string typeName,
        bool expected)
    {
        var result = HttpClientExtractor.IsPaginationBaseType(typeName);
        Assert.Equal(expected, result);
    }

    // ========== Extract with Pagination Pattern Tests ==========
    [Fact]
    public void Extract_WithPaginationResultAndItemsProperty_GeneratesGenericType()
    {
        // Arrange
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
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);
        Assert.Single(clientClass.Methods);

        var method = clientClass.Methods[0];
        Assert.Equal("GetDevicesAsync", method.Name);

        // Verify generic return type: Task<PaginationResult<Device>>
        Assert.Equal("Task", method.ReturnGenericTypeName);
        Assert.Equal("PaginationResult<Device>", method.ReturnTypeName);
    }

    [Fact]
    public void Extract_WithPaginatedResultAndResultsProperty_GeneratesGenericType()
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
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods![0];

        // Verify generic return type: Task<PaginatedResult<User>>
        Assert.Equal("Task", method.ReturnGenericTypeName);
        Assert.Equal("PaginatedResult<User>", method.ReturnTypeName);
    }

    [Fact]
    public void Extract_WithPagedResultPattern_GeneratesGenericType()
    {
        // Arrange - using "PagedResult" naming convention
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /orders:
                                get:
                                  operationId: getOrders
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            allOf:
                                              - $ref: '#/components/schemas/PagedResult'
                                              - type: object
                                                properties:
                                                  items:
                                                    type: array
                                                    items:
                                                      $ref: '#/components/schemas/Order'
                            components:
                              schemas:
                                PagedResult:
                                  type: object
                                  properties:
                                    offset:
                                      type: integer
                                    limit:
                                      type: integer
                                Order:
                                  type: object
                                  properties:
                                    orderId:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods![0];

        // Verify generic return type: Task<PagedResult<Order>>
        Assert.Equal("Task", method.ReturnGenericTypeName);
        Assert.Equal("PagedResult<Order>", method.ReturnTypeName);
    }

    [Fact]
    public void Extract_WithAsyncEnumerable_DoesNotAddEnumeratorCancellationToNonStreamingMethod()
    {
        // Arrange - pagination method with x-return-async-enumerable should NOT get EnumeratorCancellation
        // because the return type is Task<PaginationResult<T>>, not IAsyncEnumerable<T>
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
                                Device:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods![0];

        // Method should return Task<PaginationResult<Device>>, not IAsyncEnumerable
        Assert.Equal("Task", method.ReturnGenericTypeName);
        Assert.Contains("PaginationResult", method.ReturnTypeName, StringComparison.Ordinal);

        // CancellationToken parameter should NOT have EnumeratorCancellation attribute
        var ctParameter = method.Parameters?.FirstOrDefault(p => p.Name == "cancellationToken");
        Assert.NotNull(ctParameter);
        Assert.Null(ctParameter.Attributes); // No attributes for non-streaming method
    }

    [Fact]
    public void Extract_WithAsyncEnumerableArrayResponse_AddsEnumeratorCancellationAttribute()
    {
        // Arrange - simple array response with x-return-async-enumerable SHOULD get EnumeratorCancellation
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
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods![0];

        // Method should return IAsyncEnumerable<Item>
        Assert.Equal("IAsyncEnumerable", method.ReturnGenericTypeName);
        Assert.Equal("Item", method.ReturnTypeName);

        // CancellationToken parameter should have EnumeratorCancellation attribute
        var ctParameter = method.Parameters?.FirstOrDefault(p => p.Name == "cancellationToken");
        Assert.NotNull(ctParameter);
        Assert.NotNull(ctParameter.Attributes);
        Assert.Single(ctParameter.Attributes);
        Assert.Equal("EnumeratorCancellation", ctParameter.Attributes[0].Name);
    }

    // ========== JSON Serializer Options Tests ==========
    [Fact]
    public void Extract_GeneratesDefaultJsonSerializerOptionsStaticField()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /widgets:
                                get:
                                  operationId: getWidgets
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Widget'
                            components:
                              schemas:
                                Widget:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.AdditionalFieldDeclarations);
        var declarations = string.Join("\n", clientClass.AdditionalFieldDeclarations);
        Assert.Contains("defaultJsonSerializerOptions", declarations, StringComparison.Ordinal);
        Assert.Contains("JsonStringEnumConverter", declarations, StringComparison.Ordinal);
        Assert.Contains("PropertyNameCaseInsensitive", declarations, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_GeneratesTwoConstructors_WithJsonSerializerOptions()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /widgets:
                                get:
                                  operationId: getWidgets
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Widget'
                            components:
                              schemas:
                                Widget:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Constructors);
        Assert.Equal(2, clientClass.Constructors.Count);

        // Constructor 1 should have AdditionalStatements referencing defaultJsonSerializerOptions
        var ctor1 = clientClass.Constructors[0];
        Assert.NotNull(ctor1.AdditionalStatements);
        Assert.Contains("defaultJsonSerializerOptions", ctor1.AdditionalStatements[0], StringComparison.Ordinal);

        // Constructor 2 should have jsonSerializerOptions parameter
        var ctor2 = clientClass.Constructors[1];
        Assert.NotNull(ctor2.Parameters);
        Assert.Equal(2, ctor2.Parameters.Count);
        Assert.Equal("jsonSerializerOptions", ctor2.Parameters[1].Name);
    }

    [Fact]
    public void Extract_GetMethodBody_PassesJsonSerializerOptions()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /widgets:
                                get:
                                  operationId: getWidgets
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Widget'
                            components:
                              schemas:
                                Widget:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods![0];
        Assert.NotNull(method.Content);
        Assert.Contains("jsonSerializerOptions", method.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("new JsonSerializerOptions", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PostMethodBody_PassesJsonSerializerOptionsToPostAndRead()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /gadgets:
                                post:
                                  operationId: createGadget
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/CreateGadgetRequest'
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Gadget'
                            components:
                              schemas:
                                CreateGadgetRequest:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                Gadget:
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
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods![0];
        Assert.NotNull(method.Content);
        Assert.Contains("PostAsJsonAsync(url, parameters.Request, jsonSerializerOptions, cancellationToken)", method.Content, StringComparison.Ordinal);
        Assert.Contains("ReadFromJsonAsync<Gadget>(jsonSerializerOptions, cancellationToken)", method.Content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}