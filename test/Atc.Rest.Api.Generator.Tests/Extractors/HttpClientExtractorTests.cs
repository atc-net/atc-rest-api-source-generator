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
        var result = PaginationTypeResolver.IsPaginationBaseType(typeName);
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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        // Methods include the operation method + the EnsureSuccessAsync helper
        var method = clientClass.Methods.First(m => m.Name == "GetDevicesAsync");

        // Verify generic return type: Task<PaginationResult<Device>>
        Assert.Equal("Task", method.ReturnGenericTypeName);
        Assert.Equal("PaginationResult<Device>", method.ReturnTypeName);
    }

    /// <summary>
    /// Every page array name the model knows is one the client knows: the base became <c>PaginationResult&lt;T&gt;</c>
    /// for a <c>content</c>, <c>data</c> or <c>values</c> array, but the client named the page <c>PaginationResult</c>
    /// without its item type (it did not build), and a <c>records</c> array made no generic base at all.
    /// </summary>
    [Theory]
    [InlineData("items")]
    [InlineData("results")]
    [InlineData("data")]
    [InlineData("values")]
    [InlineData("content")]
    [InlineData("records")]
    public void Extract_APageOfAnyKnownArrayName_IsTheGenericBaseOfItsItems(
        string arrayName)
    {
        var document = ParseYaml($$"""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
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
                                  {{arrayName}}:
                                    type: array
                                    items:
                                      $ref: '#/components/schemas/Device'
            components:
              schemas:
                PaginationResult:
                  type: object
                  properties:
                    pageSize: { type: integer }
                    {{arrayName}}:
                      type: array
                      items: {}
                Device:
                  type: object
                  properties:
                    id: { type: string }
            """);
        Assert.NotNull(document);

        var clientClass = HttpClientExtractor.Extract(document, "TestApi", registry: null, systemTypeResolver: new SystemTypeConflictResolver([]), includeDeprecated: false);

        Assert.Equal("PaginationResult<Device>", clientClass!.Methods!.First(m => m.Name == "GetDevicesAsync").ReturnTypeName);
        Assert.True(SchemaExtractor.IsPaginationBaseSchema((OpenApiSchema)document.Components!.Schemas!["PaginationResult"]));
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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods[0];

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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods[0];

        // Verify generic return type: Task<PagedResult<Order>>
        Assert.Equal("Task", method.ReturnGenericTypeName);
        Assert.Equal("PagedResult<Order>", method.ReturnTypeName);
    }

    [Fact]
    public void Extract_WithAsyncEnumerableObjectResponse_StreamsASequenceOfThatObject()
    {
        // Arrange - a streamed response whose schema is not an array is a sequence of that schema: the server
        // returns IAsyncEnumerable<page> and the TypeScript client an AsyncGenerator<page>, so the C# client
        // reads the same stream instead of a single page
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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods[0];

        // Method returns IAsyncEnumerable<PaginationResult<Device>>
        Assert.Equal("IAsyncEnumerable", method.ReturnGenericTypeName);
        Assert.Contains("PaginationResult", method.ReturnTypeName, StringComparison.Ordinal);

        // An async iterator takes its cancellation token through [EnumeratorCancellation]
        var ctParameter = method.Parameters?.FirstOrDefault(p => p.Name == "cancellationToken");
        Assert.NotNull(ctParameter);
        Assert.NotNull(ctParameter.Attributes);
        Assert.Contains(ctParameter.Attributes, a => a.Name == "EnumeratorCancellation");
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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods[0];

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
    public void Extract_RawFileBodyOfOneMediaType_IsSentAsThatMediaType()
    {
        // The operation accepts application/octet-stream only: a text file sent as text/plain was a 415
        var document = ParseYaml(RawFileBodySpecs.Yaml("application/octet-stream"));
        Assert.NotNull(document);

        var clientClass = HttpClientExtractor.Extract(document, "TestApi", registry: null, systemTypeResolver: new SystemTypeConflictResolver([]), includeDeprecated: false);

        var content = clientClass!.Methods![0].Content;
        Assert.NotNull(content);
        Assert.Contains("content.Headers.ContentType = new MediaTypeHeaderValue(\"application/octet-stream\");", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_RawFileBodyOfSeveralMediaTypes_IsSentAsTheFilesMediaType()
    {
        var document = ParseYaml(RawFileBodySpecs.Yaml("application/pdf", "image/png"));
        Assert.NotNull(document);

        var clientClass = HttpClientExtractor.Extract(document, "TestApi", registry: null, systemTypeResolver: new SystemTypeConflictResolver([]), includeDeprecated: false);

        var content = clientClass!.Methods![0].Content;
        Assert.NotNull(content);
        Assert.Contains("content.Headers.ContentType = new MediaTypeHeaderValue(parameters.File.ContentType ?? \"application/pdf\");", content, StringComparison.Ordinal);
    }

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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.AdditionalFieldDeclarations);
        var declarations = string.Join('\n', clientClass.AdditionalFieldDeclarations);
        Assert.Contains("defaultJsonSerializerOptions", declarations, StringComparison.Ordinal);
        Assert.DoesNotContain("new JsonStringEnumConverter()", declarations, StringComparison.Ordinal);
        Assert.Contains("PropertyNameCaseInsensitive", declarations, StringComparison.Ordinal);

        // The wire names of the specification are camelCase (the models carry [JsonPropertyName] only where
        // they are not): the client must write camelCase, not the C# property names
        Assert.Contains("PropertyNamingPolicy = JsonNamingPolicy.CamelCase", declarations, StringComparison.Ordinal);
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
            document,
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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods[0];
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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods[0];
        Assert.NotNull(method.Content);
        Assert.Contains("PostAsync(url, jsonSerializerOptions.ToJsonContent(parameters.Request), cancellationToken)", method.Content, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Deserialize(json, jsonSerializerOptions.TypeInfo<Gadget>())", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_QueryMethodBody_SendsRequestWithBodyAndReadsResponse()
    {
        // Arrange - OpenAPI 3.2 `query` method: GET-like with a request body.
        const string yaml = """
                            openapi: 3.2.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /widgets:
                                query:
                                  operationId: queryWidgets
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/WidgetQuery'
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/Widget'
                            components:
                              schemas:
                                WidgetQuery:
                                  type: object
                                  properties:
                                    filter:
                                      type: string
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
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods[0];
        Assert.NotNull(method.Content);

        // QUERY is an RFC verb with a static HttpMethod property, so the typed client uses it
        // rather than allocating a new HttpMethod per call — matching the per-operation client,
        // which has always resolved verbs through EndpointMapHelper.BuildHttpMethodExpression.
        Assert.Contains("HttpMethod.Query", method.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("new HttpMethod(\"QUERY\")", method.Content, StringComparison.Ordinal);
        Assert.Contains("jsonSerializerOptions.ToJsonContent(parameters.Request)", method.Content, StringComparison.Ordinal);
        Assert.Contains("SendAsync(", method.Content, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Deserialize(json, jsonSerializerOptions.TypeInfo<Widget>())", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_CustomVerbMethodBody_SendsRequestWithoutBody()
    {
        // Arrange - OpenAPI 3.2 additionalOperations custom verb (LINK), no body, no return.
        const string yaml = """
                            openapi: 3.2.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /widgets:
                                additionalOperations:
                                  LINK:
                                    operationId: linkWidget
                                    responses:
                                      '204':
                                        description: Linked
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods[0];
        Assert.NotNull(method.Content);
        Assert.Contains("new HttpMethod(\"LINK\")", method.Content, StringComparison.Ordinal);
        Assert.Contains("SendAsync(", method.Content, StringComparison.Ordinal);
        Assert.Contains("EnsureSuccessAsync(", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ItemSchemaStreamingResponse_ReturnsAsyncEnumerableOfItem()
    {
        // Arrange - OpenAPI 3.2 itemSchema on a streaming media type (no x-* annotation).
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
                                        text/event-stream:
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
        var clientClass = HttpClientExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert — the response still surfaces as IAsyncEnumerable<Event>, but a text/event-stream
        // itemSchema now reads via the emitted StreamReaders SSE helper (not the JSON-array
        // DeserializeAsyncEnumerable path, which cannot parse `data:` framing).
        Assert.NotNull(clientClass);
        var method = clientClass.Methods[0];
        Assert.NotNull(method.Content);
        Assert.Equal("IAsyncEnumerable", method.ReturnGenericTypeName);
        Assert.Equal("Event", method.ReturnTypeName);
        Assert.Contains("StreamReaders.ReadServerSentEventsAsync<Event>", method.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("DeserializeAsyncEnumerable", method.Content, StringComparison.Ordinal);
    }

    // ========== NeedsUrlEncoding Tests ==========
    [Theory]
    [InlineData("string", true)]
    [InlineData("DateTimeOffset", true)]
    [InlineData("OrderStatus", true)]   // custom enum type
    [InlineData("MyCustomType", true)]  // unknown custom type
    [InlineData("int", false)]
    [InlineData("long", false)]
    [InlineData("short", false)]
    [InlineData("byte", false)]
    [InlineData("bool", false)]
    [InlineData("float", false)]
    [InlineData("double", false)]
    [InlineData("decimal", false)]
    [InlineData("Guid", false)]
    [InlineData("int?", false)]
    [InlineData("Guid?", false)]
    [InlineData("string?", true)]
    [InlineData("DateTimeOffset?", true)]
    [InlineData("int[]", false)]        // arrays handled separately
    public void NeedsUrlEncoding_ReturnsExpectedResult(
        string csharpType,
        bool expected)
    {
        var result = HttpClientExtractor.NeedsUrlEncoding(csharpType);
        Assert.Equal(expected, result);
    }

    // ========== BuildPathParameterReplacement Tests ==========
    [Theory]
    [InlineData("Name", "string", "{Uri.EscapeDataString(parameters.Name)}")]
    [InlineData("Status", "OrderStatus", "{Uri.EscapeDataString($\"{parameters.Status}\")}")]
    [InlineData("Date", "DateTimeOffset", "{Uri.EscapeDataString(((global::System.IFormattable?)parameters.Date)?.ToString(\"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK\", global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)}")]
    [InlineData("Day", "DateOnly", "{Uri.EscapeDataString(((global::System.IFormattable?)parameters.Day)?.ToString(\"yyyy-MM-dd\", global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)}")]
    [InlineData("Ratio", "double", "{Uri.EscapeDataString(((global::System.IFormattable?)parameters.Ratio)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)}")]
    [InlineData("Id", "int", "{Uri.EscapeDataString(((global::System.IFormattable?)parameters.Id)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)}")]
    [InlineData("Id", "Guid", "{parameters.Id}")]
    [InlineData("Count", "long", "{Uri.EscapeDataString(((global::System.IFormattable?)parameters.Count)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)}")]
    public void BuildPathParameterReplacement_ReturnsExpectedResult(
        string propName,
        string paramType,
        string expected)
    {
        var result = HttpClientExtractor.BuildPathParameterReplacement(propName, paramType);
        Assert.Equal(expected, result);
    }

    // ========== BuildEncodedExpression Tests ==========
    [Theory]
    [InlineData("parameters.Name", "string", "Uri.EscapeDataString(parameters.Name)")]
    [InlineData("parameters.Status", "OrderStatus", "Uri.EscapeDataString($\"{parameters.Status}\")")]
    [InlineData("parameters.Id", "int", "Uri.EscapeDataString(((global::System.IFormattable?)parameters.Id)?.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)")]
    [InlineData("parameters.Id", "Guid", "parameters.Id")]
    public void BuildEncodedExpression_ReturnsExpectedResult(
        string accessExpression,
        string paramType,
        string expected)
    {
        var result = HttpClientExtractor.BuildEncodedExpression(accessExpression, paramType);
        Assert.Equal(expected, result);
    }

    // ========== Array Query Parameter (form-explode repeated-key) Tests ==========
    [Fact]
    public void Extract_QueryArrayParam_Required_EmitsRepeatedKeyForeach()
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
                                  operationId: findPets
                                  parameters:
                                    - name: tags
                                      in: query
                                      required: true
                                      schema:
                                        type: array
                                        items:
                                          type: string
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods.First(m => m.Name == "FindPetsAsync");
        Assert.NotNull(method.Content);

        // Should emit repeated-key foreach
        Assert.Contains("foreach (var item in parameters.Tags)", method.Content, StringComparison.Ordinal);
        Assert.Contains("queryParams.Add($\"tags={Uri.EscapeDataString(item)}\");", method.Content, StringComparison.Ordinal);

        // Should NOT emit broken collection.ToString() pattern
        Assert.DoesNotContain("{parameters.Tags}", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_QueryArrayParam_Optional_EmitsRepeatedKeyForeachWithNullGuard()
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
                                  operationId: findPets
                                  parameters:
                                    - name: tags
                                      in: query
                                      required: false
                                      schema:
                                        type: array
                                        items:
                                          type: string
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods.First(m => m.Name == "FindPetsAsync");
        Assert.NotNull(method.Content);

        // Should have null guard before the foreach
        Assert.Contains("parameters.Tags is not null", method.Content, StringComparison.Ordinal);

        // Should emit repeated-key foreach inside the guard
        Assert.Contains("foreach (var item in parameters.Tags)", method.Content, StringComparison.Ordinal);
        Assert.Contains("queryParams.Add($\"tags={Uri.EscapeDataString(item)}\");", method.Content, StringComparison.Ordinal);

        // Should NOT emit broken collection.ToString() pattern
        Assert.DoesNotContain("{parameters.Tags}", method.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_QueryPrimitiveParam_AllowReserved_KeepsTheReservedCharactersOnly()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /search:
                                get:
                                  operationId: search
                                  parameters:
                                    - name: q
                                      in: query
                                      required: true
                                      allowReserved: true
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        var method = clientClass.Methods.First(m => m.Name == "SearchAsync");
        Assert.NotNull(method.Content);

        // The reserved characters as they are, everything else (a space, a #) encoded
        Assert.Contains($"queryParams.Add($\"q={{{WireValueHelper.AllowReservedEscapeExpression("parameters.Q!")}}}\");", method.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("queryParams.Add($\"q={parameters.Q}\");", method.Content, StringComparison.Ordinal);
    }

    // ========== Client class naming Tests ==========
    [Theory]
    [InlineData("TestApi", "TestApiClient")]
    [InlineData("Eloverblik.Api.ThirdPartyApi", "ThirdPartyApiClient")]
    [InlineData("MyCompany.Product.WebApi", "WebApiClient")]
    public void Extract_WithDottedProjectName_UsesLastNamespaceSegmentAsClassName(
        string projectName,
        string expectedClassName)
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /isalive:
                                get:
                                  operationId: getIsAlive
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document,
            projectName,
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.Equal(expectedClassName, clientClass.ClassTypeName);
        Assert.DoesNotContain(".", clientClass.ClassTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_UrlEncodedFormBody_SendsTheFieldsAsFormUrlEncodedContent()
    {
        // A URL-encoded form body was not sent at all (the method had no body). Every field is its wire text; a list is
        // one field per item.
        var document = ParseYaml(UrlEncodedFormYaml);
        Assert.NotNull(document);

        var clientClass = HttpClientExtractor.Extract(document, "TestApi", registry: null, systemTypeResolver: new SystemTypeConflictResolver([]), includeDeprecated: false);

        var content = clientClass!.Methods!.First(m => m.Name == "LoginAsync").Content!;
        Assert.Contains("var fields = new List<KeyValuePair<string, string>>();", content, StringComparison.Ordinal);
        Assert.Contains("if (parameters.Request?.User is { } user)", content, StringComparison.Ordinal);
        Assert.Contains("fields.Add(new KeyValuePair<string, string>(\"remember\", (remember ? \"true\" : \"false\")));", content, StringComparison.Ordinal);
        Assert.Contains("fields.Add(new KeyValuePair<string, string>(\"role\", role.ToWireValue()));", content, StringComparison.Ordinal);
        Assert.Contains("foreach (var item in scopes)", content, StringComparison.Ordinal);
        Assert.Contains("using var content = new FormUrlEncodedContent(fields);", content, StringComparison.Ordinal);
        Assert.Contains("httpClient.PostAsync(url, content, cancellationToken)", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("multipart/form-data; charset=utf-8", "{ $ref: '#/components/schemas/Upload' }")]
    [InlineData("MULTIPART/FORM-DATA", "{ $ref: '#/components/schemas/Upload' }")]
    public void Extract_MultipartModelBody_SendsItsFieldsAsParts(
        string mediaType,
        string schema)
    {
        // A model under a media type with a parameter or in another case was not sent at all
        var document = ParseYaml($$"""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths:
              /uploads:
                post:
                  operationId: upload
                  requestBody:
                    required: true
                    content:
                      '{{mediaType}}':
                        schema: {{schema}}
                  responses:
                    '204': { description: Uploaded }
            components:
              schemas:
                Upload:
                  type: object
                  properties:
                    name: { type: string }
                    file: { type: string, format: binary }
            """);
        Assert.NotNull(document);

        var clientClass = HttpClientExtractor.Extract(document, "TestApi", registry: null, systemTypeResolver: new SystemTypeConflictResolver([]), includeDeprecated: false);

        var content = clientClass!.Methods!.First(m => m.Name == "UploadAsync").Content!;
        Assert.Contains("using var content = new MultipartFormDataContent();", content, StringComparison.Ordinal);
        Assert.Contains("content.Add(new StringContent(", content, StringComparison.Ordinal);
        Assert.Contains(".OpenReadStream());", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// A file or multipart body is sent on every verb, not only on POST: a PUT or PATCH that uploads a photo sent no body
    /// at all (<c>PutAsync(url, null, …)</c>).
    /// </summary>
    [Theory]
    [InlineData("post", "multipart/form-data", "{ $ref: '#/components/schemas/Upload' }", "new MultipartFormDataContent()")]
    [InlineData("put", "multipart/form-data", "{ $ref: '#/components/schemas/Upload' }", "new MultipartFormDataContent()")]
    [InlineData("patch", "multipart/form-data", "{ $ref: '#/components/schemas/Upload' }", "new MultipartFormDataContent()")]
    [InlineData("post", "application/octet-stream", "{ type: string, format: binary }", "new StreamContent(parameters.File!.OpenReadStream())")]
    [InlineData("put", "application/octet-stream", "{ type: string, format: binary }", "new StreamContent(parameters.File!.OpenReadStream())")]
    [InlineData("patch", "application/octet-stream", "{ type: string, format: binary }", "new StreamContent(parameters.File!.OpenReadStream())")]
    public void Extract_FileOrMultipartBody_IsSentOnEveryVerb(
        string verb,
        string mediaType,
        string schema,
        string expectedContent)
    {
        var document = ParseYaml($$"""
            openapi: 3.0.0
            info: { title: T, version: 1.0.0 }
            paths:
              /pets/{petId}/photo:
                {{verb}}:
                  operationId: uploadPhoto
                  parameters:
                    - { name: petId, in: path, required: true, schema: { type: string } }
                  requestBody:
                    required: true
                    content:
                      '{{mediaType}}':
                        schema: {{schema}}
                  responses:
                    '204': { description: Uploaded }
            components:
              schemas:
                Upload:
                  type: object
                  properties:
                    name: { type: string }
                    file: { type: string, format: binary }
            """);
        Assert.NotNull(document);

        var clientClass = HttpClientExtractor.Extract(document, "TestApi", registry: null, systemTypeResolver: new SystemTypeConflictResolver([]), includeDeprecated: false);

        var content = clientClass!.Methods!.First(m => m.Name == "UploadPhotoAsync").Content!;
        Assert.Contains(expectedContent, content, StringComparison.Ordinal);
        Assert.DoesNotContain("(url, null,", content, StringComparison.Ordinal);
        Assert.Matches(@"(Async\(url, content,|requestMessage\.Content = content;)", content);
    }

    [Fact]
    public void Extract_WithPathSegment_KeepsOnlyThatSegmentsOperationsAndNamesTheClientAfterIt()
    {
        var document = ParseYaml(TwoSegmentsYaml);
        Assert.NotNull(document);

        var clientClass = HttpClientExtractor.Extract(
            document,
            "TestApi",
            "Pets",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false,
            useServersBasePath: false,
            resultStyle: TypedClientResultStyleType.Throw);

        Assert.NotNull(clientClass);
        Assert.Equal("PetsClient", clientClass.ClassTypeName);
        Assert.Equal("TestApi.Generated.Pets.Client", clientClass.Namespace);
        Assert.NotNull(clientClass.Methods);
        var listPets = Assert.Single(clientClass.Methods, m => m.Name == "ListPetsAsync");
        Assert.DoesNotContain(clientClass.Methods, m => m.Name == "ListUsersAsync");

        // useServersBasePath: false leaves the servers[0] path off the URL.
        Assert.Contains("var url = \"pets\";", listPets.Content, StringComparison.Ordinal);
        Assert.Equal("Task", listPets.ReturnGenericTypeName);
        Assert.Equal("List<Pet>", listPets.ReturnTypeName);
    }

    [Fact]
    public void Extract_WithPathSegmentAndServersBasePathAndResultStyle_PrependsBasePathAndWrapsInEnvelope()
    {
        var document = ParseYaml(TwoSegmentsYaml);
        Assert.NotNull(document);

        var clientClass = HttpClientExtractor.Extract(
            document,
            "TestApi",
            "Users",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false,
            useServersBasePath: true,
            resultStyle: TypedClientResultStyleType.Result);

        Assert.NotNull(clientClass);
        Assert.Equal("UsersClient", clientClass.ClassTypeName);
        var listUsers = Assert.Single(clientClass.Methods!, m => m.Name == "ListUsersAsync");
        Assert.Contains("var url = \"api/v1/users\";", listUsers.Content, StringComparison.Ordinal);
        Assert.Equal("EndpointResponse<List<User>>", listUsers.ReturnTypeName);
    }

    [Fact]
    public void ExtractParameters_WithPathSegment_ProducesClientParameterRecordsWithoutBindingAttributes()
    {
        var document = ParseYaml(TwoSegmentsYaml);
        Assert.NotNull(document);

        var records = HttpClientExtractor.ExtractParameters(
            document,
            "TestApi",
            "Pets",
            registry: null,
            includeDeprecated: false,
            validateStrategy: ValidateSpecificationStrategy.Strict);

        Assert.NotNull(records);
        var record = Assert.Single(records);
        Assert.Equal("ListPetsParameters", record.Name);
        Assert.NotNull(record.Parameters);

        // The client DTO carries the schema-derived property, but no [FromQuery] - that is server-side binding.
        var limit = Assert.Single(record.Parameters, p => p.Name == "Limit");
        Assert.Equal("int", limit.TypeName);
        Assert.True(limit.IsNullableType);
        Assert.True(limit.Attributes is null || limit.Attributes.All(a => a.Name != "FromQuery"));
    }

    [Fact]
    public void ExtractParameters_PathSegmentWithoutParameterizedOperations_ReturnsNull()
    {
        var document = ParseYaml(TwoSegmentsYaml);
        Assert.NotNull(document);

        var records = HttpClientExtractor.ExtractParameters(
            document,
            "TestApi",
            "Users",
            registry: null,
            includeDeprecated: false,
            validateStrategy: ValidateSpecificationStrategy.Strict);

        Assert.Null(records);
    }

    [Fact]
    public void ExtractParametersWithInlineEnums_InlineEnumQueryParameter_TypesTheParameterAndReportsTheEnum()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  parameters:
                                    - name: status
                                      in: query
                                      required: true
                                      schema:
                                        type: string
                                        enum:
                                          - available
                                          - sold
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (records, inlineEnums) = HttpClientExtractor.ExtractParametersWithInlineEnums(
            document,
            "TestApi",
            "Pets",
            registry: null,
            includeDeprecated: false,
            validateStrategy: ValidateSpecificationStrategy.Strict,
            namespaceSegment: string.Empty);

        Assert.NotNull(records);
        var record = Assert.Single(records);
        var status = Assert.Single(record.Parameters!);
        Assert.Equal("ListPetsParametersStatus", status.TypeName);
        Assert.False(status.IsNullableType);

        // The enum is handed back for separate file emission; an empty namespace segment flattens it to the root Models namespace.
        var inlineEnum = Assert.Single(inlineEnums);
        Assert.Equal("ListPetsParametersStatus", inlineEnum.TypeName);
        Assert.Equal("TestApi.Generated.Models", inlineEnum.EnumParameters.Namespace);
        Assert.Equal(["Available", "Sold"], inlineEnum.EnumParameters.Values.Select(v => v.Name), StringComparer.Ordinal);
    }

    // The generated EnumWireConverterFactory serves every generated enum; the non-generic JsonStringEnumConverter
    // needs runtime code generation and has no enum of its own left to serve
    [Theory]
    [InlineData("[Red, Green]")]
    [InlineData(null)]
    public void DefaultJsonSerializerOptionsInitializers_AddNoNonGenericEnumConverter(
        string? enumValues)
    {
        var yaml = $$"""
            openapi: 3.0.3
            info: { title: T, version: 1.0.0 }
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/Thing' }
            components:
              schemas:
                Thing:
                  type: object
                  properties:
                    color: { type: string{{(enumValues is null ? string.Empty : $", enum: {enumValues}")}} }
            """;

        var initializers = HttpClientExtractor.DefaultJsonSerializerOptionsInitializers(ParseYaml(yaml)!, "Contoso");

        Assert.DoesNotContain(initializers, i => i.Contains("new JsonStringEnumConverter()", StringComparison.Ordinal));
    }

    private const string TwoSegmentsYaml = """
        openapi: 3.0.0
        info: { title: T, version: 1.0.0 }
        servers:
          - url: https://example.com/api/v1
        paths:
          /pets:
            get:
              operationId: listPets
              parameters:
                - name: limit
                  in: query
                  schema: { type: integer }
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema: { type: array, items: { $ref: '#/components/schemas/Pet' } }
          /users:
            get:
              operationId: listUsers
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema: { type: array, items: { $ref: '#/components/schemas/User' } }
        components:
          schemas:
            Pet:
              type: object
              properties:
                name: { type: string }
            User:
              type: object
              properties:
                name: { type: string }
        """;

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