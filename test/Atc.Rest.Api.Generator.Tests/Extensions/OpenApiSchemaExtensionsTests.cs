namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class OpenApiSchemaExtensionsTests
{
    [Fact]
    public void ToCSharpTypeForModel_WithOneOfSingleReference_ReturnsReferencedType()
    {
        // Arrange - property with oneOf containing a single reference (common nullable pattern)
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Device:
                                  type: object
                                  properties:
                                    customer:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/IdValue'
                                IdValue:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                                      format: uuid
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var deviceSchema = document!.Components!.Schemas["Device"] as OpenApiSchema;
        Assert.NotNull(deviceSchema);

        var customerProperty = deviceSchema!.Properties["customer"];
        Assert.NotNull(customerProperty);

        // Act
        var typeName = customerProperty.ToCSharpTypeForModel(isRequired: false);

        // Assert
        Assert.Equal("IdValue?", typeName);
    }

    [Fact]
    public void ToCSharpTypeForModel_WithOneOfSingleReference_NonNullable_ReturnsNonNullableType()
    {
        // Arrange - property with oneOf containing a single reference without nullable
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Device:
                                  type: object
                                  properties:
                                    owner:
                                      oneOf:
                                        - $ref: '#/components/schemas/User'
                                User:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var deviceSchema = document!.Components!.Schemas["Device"] as OpenApiSchema;
        var ownerProperty = deviceSchema!.Properties["owner"];

        // Act
        var typeName = ownerProperty.ToCSharpTypeForModel(isRequired: true);

        // Assert
        Assert.Equal("User", typeName);
    }

    [Fact]
    public void ToCSharpTypeForModel_WithOneOfMultipleReferences_ReturnsObject()
    {
        // Arrange - property with oneOf containing multiple references (polymorphic)
        // This is a different pattern and should NOT use the single-reference optimization
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Vehicle:
                                  type: object
                                  properties:
                                    owner:
                                      oneOf:
                                        - $ref: '#/components/schemas/Person'
                                        - $ref: '#/components/schemas/Company'
                                Person:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                Company:
                                  type: object
                                  properties:
                                    companyName:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var vehicleSchema = document!.Components!.Schemas["Vehicle"] as OpenApiSchema;
        var ownerProperty = vehicleSchema!.Properties["owner"];

        // Act
        var typeName = ownerProperty.ToCSharpTypeForModel(isRequired: true);

        // Assert - multiple oneOf items should fall through to default behavior
        // The exact type depends on how the generator handles polymorphic types
        // but it should NOT be "Person" or "Company" (single type)
        Assert.DoesNotContain("Person", typeName, StringComparison.Ordinal);
        Assert.DoesNotContain("Company", typeName, StringComparison.Ordinal);
    }

    [Fact]
    public void ToCSharpTypeForModel_WithAllOfSingleReference_ReturnsReferencedType()
    {
        // Arrange - property with allOf containing a single reference (existing pattern)
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Device:
                                  type: object
                                  properties:
                                    settings:
                                      nullable: true
                                      allOf:
                                        - $ref: '#/components/schemas/DeviceSettings'
                                DeviceSettings:
                                  type: object
                                  properties:
                                    enabled:
                                      type: boolean
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var deviceSchema = document!.Components!.Schemas["Device"] as OpenApiSchema;
        var settingsProperty = deviceSchema!.Properties["settings"];

        // Act
        var typeName = settingsProperty.ToCSharpTypeForModel(isRequired: false);

        // Assert
        Assert.Equal("DeviceSettings?", typeName);
    }

    [Fact]
    public void ToCSharpTypeForModel_WithDirectReference_ReturnsReferencedType()
    {
        // Arrange - property with direct $ref (simple case)
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Device:
                                  type: object
                                  properties:
                                    deviceType:
                                      $ref: '#/components/schemas/DeviceType'
                                DeviceType:
                                  type: string
                                  enum:
                                    - TypeA
                                    - TypeB
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var deviceSchema = document!.Components!.Schemas["Device"] as OpenApiSchema;
        var deviceTypeProperty = deviceSchema!.Properties["deviceType"];

        // Act
        var typeName = deviceTypeProperty.ToCSharpTypeForModel(isRequired: true);

        // Assert
        Assert.Equal("DeviceType", typeName);
    }

    [Fact]
    public void ToCSharpTypeForModel_WithOneOfAndEnumReference_ReturnsEnumType()
    {
        // Arrange - property with oneOf containing a single enum reference
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Device:
                                  type: object
                                  properties:
                                    suggestedType:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/DeviceType'
                                DeviceType:
                                  type: string
                                  enum:
                                    - TypeA
                                    - TypeB
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var deviceSchema = document!.Components!.Schemas["Device"] as OpenApiSchema;
        var suggestedTypeProperty = deviceSchema!.Properties["suggestedType"];

        // Act
        var typeName = suggestedTypeProperty.ToCSharpTypeForModel(isRequired: false);

        // Assert
        Assert.Equal("DeviceType?", typeName);
    }

    [Fact]
    public void SchemaExtractor_WithOneOfSingleReference_GeneratesCorrectRecordProperty()
    {
        // Arrange - full integration test using SchemaExtractor
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
                            components:
                              schemas:
                                InsightDevice:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                                      format: uuid
                                    name:
                                      type: string
                                    customer:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/IdValue'
                                    site:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/IdValue'
                                    suggestedCustomer:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/KeyValue'
                                IdValue:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                                      format: uuid
                                    name:
                                      type: string
                                KeyValue:
                                  type: object
                                  properties:
                                    key:
                                      type: string
                                    value:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act - use overload without pathSegment to extract all component schemas
        var recordsParams = SchemaExtractor.Extract(
            document!,
            "TestApi",
            includeDeprecated: false);

        // Assert
        Assert.NotNull(recordsParams);

        var insightDeviceRecord = recordsParams.Parameters
            .FirstOrDefault(r => r.Name == "InsightDevice");
        Assert.NotNull(insightDeviceRecord);
        Assert.NotNull(insightDeviceRecord.Parameters);

        // Check the customer property type
        var customerParam = insightDeviceRecord.Parameters
            .FirstOrDefault(p => p.Name == "Customer");
        Assert.NotNull(customerParam);
        Assert.Equal("IdValue", customerParam.TypeName);
        Assert.True(customerParam.IsNullableType);

        // Check the site property type
        var siteParam = insightDeviceRecord.Parameters
            .FirstOrDefault(p => p.Name == "Site");
        Assert.NotNull(siteParam);
        Assert.Equal("IdValue", siteParam.TypeName);
        Assert.True(siteParam.IsNullableType);

        // Check the suggestedCustomer property type
        var suggestedCustomerParam = insightDeviceRecord.Parameters
            .FirstOrDefault(p => p.Name == "SuggestedCustomer");
        Assert.NotNull(suggestedCustomerParam);
        Assert.Equal("KeyValue", suggestedCustomerParam.TypeName);
        Assert.True(suggestedCustomerParam.IsNullableType);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}