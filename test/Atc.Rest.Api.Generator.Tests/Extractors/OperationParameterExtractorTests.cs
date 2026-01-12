namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OperationParameterExtractorTests
{
    [Fact]
    public void Extract_WithEnumSchemaReference_GeneratesEnumType()
    {
        // Arrange - parameter with schema reference to an enum
        var yaml = """
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
        var yaml = """
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
        var yaml = """
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
            "Searches",  // /search gets pluralized to Searches
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
        var yaml = """
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
        var yaml = """
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
        var yaml = """
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
    public void Extract_WithArrayOfEnumSchemaReference_GeneratesListOfEnumType()
    {
        // Arrange - parameter with array of enum type
        var yaml = """
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

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}