namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OperationParameterExtractorTests
{
    [Fact]
    public void Extract_WithEnumSchemaReference_GeneratesEnumType()
    {
        // Arrange - parameter with schema reference to an enum
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /devices:
                                get:
                                  operationId: getDevices
                                  parameters:
                                    - name: deviceType
                                      in: query
                                      required: false
                                      schema:
                                        $ref: '#/components/schemas/DeviceType'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                DeviceType:
                                  type: string
                                  enum:
                                    - Unknown
                                    - TypeA
                                    - TypeB
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.NotNull(recordsParams);
        Assert.Single(recordsParams.Parameters);

        var record = recordsParams.Parameters[0];
        Assert.NotNull(record.Parameters);
        Assert.Single(record.Parameters);

        var param = record.Parameters[0];
        Assert.Equal("DeviceType", param.Name);
        Assert.Equal("DeviceType", param.TypeName);
        Assert.True(param.IsNullableType);
    }

    [Fact]
    public void Extract_WithRequiredEnumSchemaReference_GeneratesNonNullableEnumType()
    {
        // Arrange - required parameter with schema reference to an enum
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /devices:
                                get:
                                  operationId: getDevices
                                  parameters:
                                    - name: status
                                      in: query
                                      required: true
                                      schema:
                                        $ref: '#/components/schemas/Status'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                Status:
                                  type: string
                                  enum:
                                    - Active
                                    - Inactive
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.NotNull(recordsParams);
        var param = recordsParams.Parameters[0].Parameters![0];
        Assert.Equal("Status", param.Name);
        Assert.Equal("Status", param.TypeName);
        Assert.False(param.IsNullableType); // Required, so not nullable
    }

    [Fact]
    public void Extract_WithObjectSchemaReference_GeneratesObjectType()
    {
        // Arrange - parameter with schema reference to an object
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /search:
                                post:
                                  operationId: search
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/SearchRequest'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                SearchRequest:
                                  type: object
                                  properties:
                                    query:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Search",  // /search preserved as Search (no pluralization)
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.NotNull(recordsParams);
        var param = recordsParams.Parameters[0].Parameters![0];
        Assert.Equal("Request", param.Name);
        Assert.Equal("SearchRequest", param.TypeName);
    }

    [Fact]
    public void Extract_WithMultipleEnumReferences_GeneratesCorrectTypes()
    {
        // Arrange - multiple parameters with different enum references
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /devices:
                                get:
                                  operationId: getDevices
                                  parameters:
                                    - name: filter
                                      in: query
                                      schema:
                                        type: string
                                    - name: deviceType
                                      in: query
                                      schema:
                                        $ref: '#/components/schemas/DeviceType'
                                    - name: status
                                      in: query
                                      schema:
                                        $ref: '#/components/schemas/DeviceStatus'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                DeviceType:
                                  type: string
                                  enum:
                                    - TypeA
                                    - TypeB
                                DeviceStatus:
                                  type: string
                                  enum:
                                    - Online
                                    - Offline
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.NotNull(recordsParams);
        var parameters = recordsParams.Parameters[0].Parameters!;
        Assert.Equal(3, parameters.Count);

        Assert.Equal("Filter", parameters[0].Name);
        Assert.Equal("string", parameters[0].TypeName);
        Assert.True(parameters[0].IsNullableType);

        Assert.Equal("DeviceType", parameters[1].Name);
        Assert.Equal("DeviceType", parameters[1].TypeName);
        Assert.True(parameters[1].IsNullableType);

        Assert.Equal("Status", parameters[2].Name);
        Assert.Equal("DeviceStatus", parameters[2].TypeName);
        Assert.True(parameters[2].IsNullableType);
    }

    [Fact]
    public void Extract_WithSharedSchemas_IncludesSharedModelsUsing()
    {
        // Arrange - operation that uses global schemas (shared across path segments)
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /devices:
                                get:
                                  operationId: getDevices
                                  parameters:
                                    - name: deviceType
                                      in: query
                                      schema:
                                        $ref: '#/components/schemas/DeviceType'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                DeviceType:
                                  type: string
                                  enum:
                                    - TypeA
                                    - TypeB
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - includeSharedModelsUsing = true simulates having shared schemas
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Devices",
            registry: null,
            includeDeprecated: false,
            includeSharedModelsUsing: true);

        // Assert
        Assert.NotNull(recordsParams);
        Assert.NotNull(recordsParams.HeaderContent);

        // Should include the shared models namespace
        Assert.Contains("using TestApi.Generated.Models;", recordsParams.HeaderContent, StringComparison.Ordinal);

        // Should also include the path-segment-specific namespace
        Assert.Contains("using TestApi.Generated.Devices.Models;", recordsParams.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithoutSharedSchemas_DoesNotIncludeSharedModelsUsing()
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
                                  parameters:
                                    - name: filter
                                      in: query
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - includeSharedModelsUsing = false (default)
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.NotNull(recordsParams);
        Assert.NotNull(recordsParams.HeaderContent);

        // Should NOT include the shared models namespace (only one using for models)
        var modelsUsingCount = recordsParams.HeaderContent
            .Split('\n')
            .Count(line => line.Contains("using TestApi.Generated", StringComparison.Ordinal) &&
                           line.Contains("Models", StringComparison.Ordinal));
        Assert.Equal(1, modelsUsingCount);
    }

    [Fact]
    public void Extract_WithSegmentModelsDisabled_DoesNotIncludeSegmentModelsUsing()
    {
        // Arrange - endpoint that uses only shared schemas (no segment-specific models)
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /accounts:
                                get:
                                  operationId: getAccounts
                                  parameters:
                                    - name: filter
                                      in: query
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - includeSegmentModelsUsing = false (no segment-specific models exist)
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Accounts",
            registry: null,
            includeDeprecated: false,
            includeSharedModelsUsing: true,
            includeSegmentModelsUsing: false);

        // Assert
        Assert.NotNull(recordsParams);
        Assert.NotNull(recordsParams.HeaderContent);

        // Should include shared models namespace
        Assert.Contains("using TestApi.Generated.Models;", recordsParams.HeaderContent, StringComparison.Ordinal);

        // Should NOT include segment-specific models namespace
        Assert.DoesNotContain("using TestApi.Generated.Accounts.Models;", recordsParams.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithSegmentModelsEnabled_IncludesSegmentModelsUsing()
    {
        // Arrange - endpoint with segment-specific models
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /devices:
                                get:
                                  operationId: getDevices
                                  parameters:
                                    - name: deviceType
                                      in: query
                                      schema:
                                        $ref: '#/components/schemas/DeviceType'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                DeviceType:
                                  type: string
                                  enum:
                                    - TypeA
                                    - TypeB
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - includeSegmentModelsUsing = true (default, segment has its own models)
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Devices",
            registry: null,
            includeDeprecated: false,
            includeSharedModelsUsing: false,
            includeSegmentModelsUsing: true);

        // Assert
        Assert.NotNull(recordsParams);
        Assert.NotNull(recordsParams.HeaderContent);

        // Should include segment-specific models namespace
        Assert.Contains("using TestApi.Generated.Devices.Models;", recordsParams.HeaderContent, StringComparison.Ordinal);

        // Should NOT include shared models namespace (not requested)
        Assert.DoesNotContain("using TestApi.Generated.Models;", recordsParams.HeaderContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithBothSharedAndSegmentModelsDisabled_IncludesNoModelsUsing()
    {
        // Arrange - endpoint without any models (unlikely but testing edge case)
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
                                    - name: verbose
                                      in: query
                                      schema:
                                        type: boolean
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - both flags disabled
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Items",
            registry: null,
            includeDeprecated: false,
            includeSharedModelsUsing: false,
            includeSegmentModelsUsing: false);

        // Assert
        Assert.NotNull(recordsParams);
        Assert.NotNull(recordsParams.HeaderContent);

        // Should NOT include any models namespace
        var modelsUsingCount = recordsParams.HeaderContent
            .Split('\n')
            .Count(line => line.Contains("using TestApi.Generated", StringComparison.Ordinal) &&
                           line.Contains("Models", StringComparison.Ordinal));
        Assert.Equal(0, modelsUsingCount);
    }

    [Fact]
    public void Extract_WithBothSharedAndSegmentModels_IncludesBothUsings()
    {
        // Arrange - endpoint using both shared and segment-specific schemas
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /devices:
                                get:
                                  operationId: getDevices
                                  parameters:
                                    - name: deviceType
                                      in: query
                                      schema:
                                        $ref: '#/components/schemas/DeviceType'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                DeviceType:
                                  type: string
                                  enum:
                                    - TypeA
                                    - TypeB
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - both flags enabled
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Devices",
            registry: null,
            includeDeprecated: false,
            includeSharedModelsUsing: true,
            includeSegmentModelsUsing: true);

        // Assert
        Assert.NotNull(recordsParams);
        Assert.NotNull(recordsParams.HeaderContent);

        // Should include both namespaces
        Assert.Contains("using TestApi.Generated.Models;", recordsParams.HeaderContent, StringComparison.Ordinal);
        Assert.Contains("using TestApi.Generated.Devices.Models;", recordsParams.HeaderContent, StringComparison.Ordinal);

        // Should have exactly 2 models using statements
        var modelsUsingCount = recordsParams.HeaderContent
            .Split('\n')
            .Count(line => line.Contains("using TestApi.Generated", StringComparison.Ordinal) &&
                           line.Contains("Models", StringComparison.Ordinal));
        Assert.Equal(2, modelsUsingCount);
    }

    [Fact]
    public void Extract_WithArrayOfEnumSchemaReference_GeneratesListOfEnumType()
    {
        // Arrange - parameter with array of enum type
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /devices:
                                get:
                                  operationId: getDevices
                                  parameters:
                                    - name: types
                                      in: query
                                      schema:
                                        type: array
                                        items:
                                          $ref: '#/components/schemas/DeviceType'
                                  responses:
                                    '200':
                                      description: OK
                            components:
                              schemas:
                                DeviceType:
                                  type: string
                                  enum:
                                    - TypeA
                                    - TypeB
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Devices",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.NotNull(recordsParams);
        var param = recordsParams.Parameters[0].Parameters![0];
        Assert.Equal("Types", param.Name);

        // For arrays, TypeName is the full generic type
        Assert.Equal("List<DeviceType>", param.TypeName);
    }

    [Fact]
    public void Extract_WithRequestBodyWithoutExplicitRequired_DefaultsToRequired()
    {
        // Arrange - request body without explicit 'required' property
        // Per our generator convention (deviating from OpenAPI spec), defaults to required
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                post:
                                  operationId: createItem
                                  requestBody:
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/CreateItemRequest'
                                  responses:
                                    '201':
                                      description: Created
                            components:
                              schemas:
                                CreateItemRequest:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Items",
            registry: null,
            includeDeprecated: false);

        // Assert
        Assert.NotNull(recordsParams);
        var param = recordsParams.Parameters[0].Parameters![0];
        Assert.Equal("Request", param.Name);
        Assert.Equal("CreateItemRequest", param.TypeName);
        Assert.False(param.IsNullableType); // Should NOT be nullable (required by default)
        Assert.NotNull(param.Attributes);
        Assert.Contains(param.Attributes, a => a.Name == "Required"); // Should have Required attribute
    }

    [Fact]
    public void Extract_WithRequestBodyExplicitlyNotRequired_StillTreatsAsRequired()
    {
        // Arrange - request body with explicit 'required: false'
        // Note: We intentionally ignore the required field and always treat request bodies as required
        // to match old generator behavior and because we can't distinguish between
        // "explicitly false" and "defaulted to false" in the OpenAPI parser.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                post:
                                  operationId: createItem
                                  requestBody:
                                    required: false
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/CreateItemRequest'
                                  responses:
                                    '201':
                                      description: Created
                            components:
                              schemas:
                                CreateItemRequest:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var recordsParams = OperationParameterExtractor.Extract(
            document!,
            "TestApi",
            "Items",
            registry: null,
            includeDeprecated: false);

        // Assert - request bodies are always required, even if spec says required: false
        Assert.NotNull(recordsParams);
        var param = recordsParams.Parameters[0].Parameters![0];
        Assert.Equal("Request", param.Name);
        Assert.Equal("CreateItemRequest", param.TypeName);
        Assert.False(param.IsNullableType); // Always non-nullable (required)
        Assert.NotNull(param.Attributes);
        Assert.Contains(param.Attributes, a => a.Name == "Required"); // Always has Required attribute
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}