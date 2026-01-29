namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class InlineEnumExtractorTests
{
    [Fact]
    public void IsInlineEnumSchema_StringWithEnumValues_ReturnsTrue()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestSchema:
                                  type: object
                                  properties:
                                    status:
                                      type: string
                                      enum:
                                        - Active
                                        - Inactive
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var testSchema = document!.Components!.Schemas!["TestSchema"] as OpenApiSchema;
        Assert.NotNull(testSchema);

        var statusProperty = testSchema!.Properties!["status"];

        // Act
        var result = InlineEnumExtractor.IsInlineEnumSchema(statusProperty);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsInlineEnumSchema_SchemaReference_ReturnsFalse()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Status:
                                  type: string
                                  enum:
                                    - Active
                                    - Inactive
                                TestSchema:
                                  type: object
                                  properties:
                                    status:
                                      $ref: '#/components/schemas/Status'
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var testSchema = document!.Components!.Schemas!["TestSchema"] as OpenApiSchema;
        Assert.NotNull(testSchema);

        var statusProperty = testSchema!.Properties!["status"];

        // Act
        var result = InlineEnumExtractor.IsInlineEnumSchema(statusProperty);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsInlineEnumSchema_StringWithoutEnumValues_ReturnsFalse()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestSchema:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var testSchema = document!.Components!.Schemas!["TestSchema"] as OpenApiSchema;
        Assert.NotNull(testSchema);

        var nameProperty = testSchema!.Properties!["name"];

        // Act
        var result = InlineEnumExtractor.IsInlineEnumSchema(nameProperty);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsInlineEnumSchema_IntegerType_ReturnsFalse()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestSchema:
                                  type: object
                                  properties:
                                    count:
                                      type: integer
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var testSchema = document!.Components!.Schemas!["TestSchema"] as OpenApiSchema;
        Assert.NotNull(testSchema);

        var countProperty = testSchema!.Properties!["count"];

        // Act
        var result = InlineEnumExtractor.IsInlineEnumSchema(countProperty);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsInlineEnumSchema_NullSchema_ReturnsFalse()
    {
        // Act
        var result = InlineEnumExtractor.IsInlineEnumSchema(null);

        // Assert
        Assert.False(result);
    }

    // ========== GenerateInlineEnumTypeName Tests ==========
    [Theory]
    [InlineData("ResendEventsRequest", "resourceType", "ResendEventsRequestResourceType")]
    [InlineData("Order", "status", "OrderStatus")]
    [InlineData("User", "role", "UserRole")]
    [InlineData("MySchema", "myProperty", "MySchemaMyProperty")]
    [InlineData("Test", "ABC", "TestABC")]
    public void GenerateInlineEnumTypeName_StandardProperty_CombinesNames(
        string parentSchemaName,
        string propertyName,
        string expected)
    {
        // Act
        var result = InlineEnumExtractor.GenerateInlineEnumTypeName(parentSchemaName, propertyName);

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== GetEnumValuesKey Tests ==========
    [Fact]
    public void GetEnumValuesKey_SameValuesDifferentOrder_ReturnsSameKey()
    {
        // Arrange
        const string yaml1 = """
                             openapi: 3.0.0
                             info:
                               title: Test API
                               version: 1.0.0
                             paths: {}
                             components:
                               schemas:
                                 Schema1:
                                   type: object
                                   properties:
                                     status:
                                       type: string
                                       enum:
                                         - Active
                                         - Inactive
                                         - Pending
                             """;

        const string yaml2 = """
                             openapi: 3.0.0
                             info:
                               title: Test API
                               version: 1.0.0
                             paths: {}
                             components:
                               schemas:
                                 Schema2:
                                   type: object
                                   properties:
                                     state:
                                       type: string
                                       enum:
                                         - Pending
                                         - Active
                                         - Inactive
                             """;

        var document1 = ParseYaml(yaml1);
        var document2 = ParseYaml(yaml2);
        Assert.NotNull(document1);
        Assert.NotNull(document2);

        var schema1 = document1!.Components!.Schemas!["Schema1"] as OpenApiSchema;
        var schema2 = document2!.Components!.Schemas!["Schema2"] as OpenApiSchema;
        Assert.NotNull(schema1);
        Assert.NotNull(schema2);

        var status = schema1!.Properties!["status"] as OpenApiSchema;
        var state = schema2!.Properties!["state"] as OpenApiSchema;
        Assert.NotNull(status);
        Assert.NotNull(state);

        // Act
        var key1 = InlineEnumExtractor.GetEnumValuesKey(status!);
        var key2 = InlineEnumExtractor.GetEnumValuesKey(state!);

        // Assert
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GetEnumValuesKey_DifferentValues_ReturnsDifferentKeys()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestSchema:
                                  type: object
                                  properties:
                                    status1:
                                      type: string
                                      enum:
                                        - Active
                                        - Inactive
                                    status2:
                                      type: string
                                      enum:
                                        - Enabled
                                        - Disabled
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var testSchema = document!.Components!.Schemas!["TestSchema"] as OpenApiSchema;
        Assert.NotNull(testSchema);

        var status1 = testSchema!.Properties!["status1"] as OpenApiSchema;
        var status2 = testSchema!.Properties!["status2"] as OpenApiSchema;
        Assert.NotNull(status1);
        Assert.NotNull(status2);

        // Act
        var key1 = InlineEnumExtractor.GetEnumValuesKey(status1!);
        var key2 = InlineEnumExtractor.GetEnumValuesKey(status2!);

        // Assert
        Assert.NotEqual(key1, key2);
    }

    // ========== ExtractEnumFromInlineSchema Tests ==========
    [Fact]
    public void ExtractEnumFromInlineSchema_SimpleValues_CreatesEnumParameters()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestSchema:
                                  type: object
                                  properties:
                                    resourceType:
                                      type: string
                                      enum:
                                        - Account
                                        - Device
                                        - DeviceSetting
                                        - TelemetryConfiguration
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var testSchema = document!.Components!.Schemas!["TestSchema"] as OpenApiSchema;
        Assert.NotNull(testSchema);

        var resourceType = testSchema!.Properties!["resourceType"] as OpenApiSchema;
        Assert.NotNull(resourceType);

        // Act
        var result = InlineEnumExtractor.ExtractEnumFromInlineSchema(
            resourceType!,
            "TestSchemaResourceType",
            "Test.Generated.Models");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestSchemaResourceType", result.EnumTypeName);
        Assert.Equal("Test.Generated.Models", result.Namespace);
        Assert.Equal(4, result.Values.Count);
        Assert.Contains(result.Values, v => v.Name == "Account");
        Assert.Contains(result.Values, v => v.Name == "Device");
        Assert.Contains(result.Values, v => v.Name == "DeviceSetting");
        Assert.Contains(result.Values, v => v.Name == "TelemetryConfiguration");
    }

    [Fact]
    public void ExtractEnumFromInlineSchema_CamelCaseValues_AddsEnumMemberAttribute()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestSchema:
                                  type: object
                                  properties:
                                    status:
                                      type: string
                                      enum:
                                        - active
                                        - inactive
                                        - pendingApproval
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var testSchema = document!.Components!.Schemas!["TestSchema"] as OpenApiSchema;
        Assert.NotNull(testSchema);

        var status = testSchema!.Properties!["status"] as OpenApiSchema;
        Assert.NotNull(status);

        // Act
        var result = InlineEnumExtractor.ExtractEnumFromInlineSchema(
            status!,
            "TestSchemaStatus",
            "Test.Generated.Models");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Values.Count);

        // camelCase values should have EnumMember attribute with original value
        var activeValue = result.Values.First(v => v.Name == "Active");
        Assert.Equal("active", activeValue.EnumMemberValue);

        var pendingValue = result.Values.First(v => v.Name == "PendingApproval");
        Assert.Equal("pendingApproval", pendingValue.EnumMemberValue);
    }

    [Fact]
    public void ExtractEnumFromInlineSchema_WithDescription_IncludesDocumentation()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestSchema:
                                  type: object
                                  properties:
                                    status:
                                      type: string
                                      description: The current status of the resource
                                      enum:
                                        - Active
                                        - Inactive
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var testSchema = document!.Components!.Schemas!["TestSchema"] as OpenApiSchema;
        Assert.NotNull(testSchema);

        var status = testSchema!.Properties!["status"] as OpenApiSchema;
        Assert.NotNull(status);

        // Act
        var result = InlineEnumExtractor.ExtractEnumFromInlineSchema(
            status!,
            "TestSchemaStatus",
            "Test.Generated.Models");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.DocumentationTags);
        Assert.Equal("The current status of the resource", result.DocumentationTags.Summary);
    }

    [Fact]
    public void ExtractEnumFromInlineSchema_EmptyEnum_ReturnsNull()
    {
        // Arrange - create a schema with no enum values
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Enum = [],
        };

        // Act
        var result = InlineEnumExtractor.ExtractEnumFromInlineSchema(
            schema,
            "TestEnum",
            "Test.Generated.Models");

        // Assert
        Assert.Null(result);
    }

    // ========== Integration with SchemaExtractor Tests ==========
    [Fact]
    public void SchemaExtractor_WithInlineEnum_ExtractsEnumAndUpdatesPropertyType()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                ResendEventsRequest:
                                  type: object
                                  required:
                                    - resourceType
                                  properties:
                                    resourceType:
                                      type: string
                                      enum:
                                        - Account
                                        - Device
                                        - DeviceSetting
                                        - TelemetryConfiguration
                                    ids:
                                      type: array
                                      items:
                                        type: string
                                        format: uuid
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "ResendEventsRequest" };

        // Act
        var (records, inlineEnums) = SchemaExtractor.ExtractForSchemasWithInlineEnums(
            document!,
            "TestApi",
            schemaNames,
            "Events",
            registry: null,
            includeDeprecated: false,
            generatePartialModels: false,
            includeSharedModelsUsing: false);

        // Assert
        Assert.NotNull(records);
        Assert.Single(records.Parameters);
        Assert.Single(inlineEnums);

        // Check inline enum
        var inlineEnum = inlineEnums[0];
        Assert.Equal("ResendEventsRequestResourceType", inlineEnum.TypeName);
        Assert.Equal("Events", inlineEnum.PathSegment);
        Assert.Equal(4, inlineEnum.EnumParameters.Values.Count);

        // Check record property uses enum type
        var record = records.Parameters[0];
        var resourceTypeProp = record.Parameters?.FirstOrDefault(p => p.Name == "ResourceType");
        Assert.NotNull(resourceTypeProp);
        Assert.Equal("ResendEventsRequestResourceType", resourceTypeProp.TypeName);
    }

    [Fact]
    public void SchemaExtractor_WithDuplicateInlineEnumValues_DeduplicatesEnums()
    {
        // Arrange - Two properties with the same enum values should share the same enum type
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Request1:
                                  type: object
                                  properties:
                                    status:
                                      type: string
                                      enum:
                                        - Active
                                        - Inactive
                                Request2:
                                  type: object
                                  properties:
                                    state:
                                      type: string
                                      enum:
                                        - Active
                                        - Inactive
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "Request1", "Request2" };

        // Act
        var (records, inlineEnums) = SchemaExtractor.ExtractForSchemasWithInlineEnums(
            document!,
            "TestApi",
            schemaNames,
            "Test",
            registry: null,
            includeDeprecated: false,
            generatePartialModels: false,
            includeSharedModelsUsing: false);

        // Assert - Should only have one unique inline enum due to deduplication
        Assert.Single(inlineEnums);

        // Both records should use the same enum type (the first one created)
        Assert.NotNull(records);
        Assert.Equal(2, records.Parameters.Count);

        var request1 = records.Parameters.First(r => r.Name == "Request1");
        var request2 = records.Parameters.First(r => r.Name == "Request2");

        var status1 = request1.Parameters?.FirstOrDefault(p => p.Name == "Status");
        var status2 = request2.Parameters?.FirstOrDefault(p => p.Name == "State");

        Assert.NotNull(status1);
        Assert.NotNull(status2);

        // Both should use the first generated enum type name
        Assert.Equal(status1.TypeName, status2.TypeName);
    }

    [Fact]
    public void SchemaExtractor_OptionalInlineEnum_IsNullable()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestRequest:
                                  type: object
                                  properties:
                                    optionalStatus:
                                      type: string
                                      enum:
                                        - Active
                                        - Inactive
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestRequest" };

        // Act
        var (records, inlineEnums) = SchemaExtractor.ExtractForSchemasWithInlineEnums(
            document!,
            "TestApi",
            schemaNames,
            "Test",
            registry: null,
            includeDeprecated: false,
            generatePartialModels: false,
            includeSharedModelsUsing: false);

        // Assert
        Assert.NotNull(records);
        Assert.Single(records.Parameters);
        Assert.Single(inlineEnums);

        var record = records.Parameters[0];
        var statusProp = record.Parameters?.FirstOrDefault(p => p.Name == "OptionalStatus");
        Assert.NotNull(statusProp);

        // Optional enum properties should be nullable
        Assert.True(statusProp.IsNullableType);
    }

    [Fact]
    public void SchemaExtractor_RequiredInlineEnum_IsNotNullable()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                TestRequest:
                                  type: object
                                  required:
                                    - requiredStatus
                                  properties:
                                    requiredStatus:
                                      type: string
                                      enum:
                                        - Active
                                        - Inactive
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var schemaNames = new HashSet<string>(StringComparer.Ordinal) { "TestRequest" };

        // Act
        var (records, inlineEnums) = SchemaExtractor.ExtractForSchemasWithInlineEnums(
            document!,
            "TestApi",
            schemaNames,
            "Test",
            registry: null,
            includeDeprecated: false,
            generatePartialModels: false,
            includeSharedModelsUsing: false);

        // Assert
        Assert.NotNull(records);
        Assert.Single(records.Parameters);

        var record = records.Parameters[0];
        var statusProp = record.Parameters?.FirstOrDefault(p => p.Name == "RequiredStatus");
        Assert.NotNull(statusProp);

        // Required enum properties should not be nullable
        Assert.False(statusProp.IsNullableType);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}