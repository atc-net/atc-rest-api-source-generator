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

    // ========== SanitizeSchemaName Tests ==========
    [Theory]
    [InlineData("Pet", "Pet")]
    [InlineData("Foo.Bar.Baz", "Foo_Bar_Baz")]
    [InlineData("Simple", "Simple")]
    [InlineData("A.B", "A_B")]
    [InlineData("", "")]
    public void SanitizeSchemaName_ReturnsExpected(
        string input,
        string expected)
    {
        var result = OpenApiSchemaExtensions.SanitizeSchemaName(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeSchemaName_NullInput_ReturnsNull()
    {
        var result = OpenApiSchemaExtensions.SanitizeSchemaName(null!);
        Assert.Null(result);
    }

    // ========== ResolveTypeName Tests ==========
    [Fact]
    public void ResolveTypeName_WithDots_SanitizesToUnderscores()
    {
        var result = OpenApiSchemaExtensions.ResolveTypeName("Foo.Bar");
        Assert.Equal("Foo_Bar", result);
    }

    [Fact]
    public void ResolveTypeName_NoRegistry_ReturnsSanitized()
    {
        var result = OpenApiSchemaExtensions.ResolveTypeName(
            "Pet",
            null);
        Assert.Equal("Pet", result);
    }

    // ========== GetSchemaType Tests ==========
    [Fact]
    public void GetSchemaType_StringSchema_ReturnsString()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        Assert.Equal("string", schema.GetSchemaType());
    }

    [Fact]
    public void GetSchemaType_IntegerSchema_ReturnsInteger()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
        Assert.Equal("integer", schema.GetSchemaType());
    }

    [Fact]
    public void GetSchemaType_ArraySchema_ReturnsArray()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Array };
        Assert.Equal("array", schema.GetSchemaType());
    }

    [Fact]
    public void GetSchemaType_ObjectSchema_ReturnsObject()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };
        Assert.Equal("object", schema.GetSchemaType());
    }

    [Fact]
    public void GetSchemaType_NullableString_ReturnsString()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null };
        Assert.Equal("string", schema.GetSchemaType());
    }

    [Fact]
    public void GetSchemaType_NoType_ReturnsNull()
    {
        var schema = new OpenApiSchema();
        Assert.Null(schema.GetSchemaType());
    }

    [Fact]
    public void GetSchemaType_BooleanSchema_ReturnsBoolean()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Boolean };
        Assert.Equal("boolean", schema.GetSchemaType());
    }

    [Fact]
    public void GetSchemaType_NumberSchema_ReturnsNumber()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Number };
        Assert.Equal("number", schema.GetSchemaType());
    }

    // ========== IsNullable Tests ==========
    [Fact]
    public void IsNullable_WithNullFlag_ReturnsTrue()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null };
        Assert.True(schema.IsNullable());
    }

    [Fact]
    public void IsNullable_WithoutNullFlag_ReturnsFalse()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        Assert.False(schema.IsNullable());
    }

    [Fact]
    public void IsNullable_NoType_ReturnsFalse()
    {
        var schema = new OpenApiSchema();
        Assert.False(schema.IsNullable());
    }

    // ========== HasMultipleNonNullTypes Tests ==========
    [Fact]
    public void HasMultipleNonNullTypes_SingleType_ReturnsFalse()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        Assert.False(schema.HasMultipleNonNullTypes());
    }

    [Fact]
    public void HasMultipleNonNullTypes_StringAndInteger_ReturnsTrue()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Integer };
        Assert.True(schema.HasMultipleNonNullTypes());
    }

    [Fact]
    public void HasMultipleNonNullTypes_StringAndNull_ReturnsFalse()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null };
        Assert.False(schema.HasMultipleNonNullTypes());
    }

    // ========== GetCSharpTypeName Tests ==========
    [Fact]
    public void GetCSharpTypeName_IntegerSchema_ReturnsInt()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
        Assert.Equal("int", schema.GetCSharpTypeName());
    }

    [Fact]
    public void GetCSharpTypeName_Int64Format_ReturnsLong()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" };
        Assert.Equal("long", schema.GetCSharpTypeName());
    }

    [Fact]
    public void GetCSharpTypeName_StringSchema_ReturnsString()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        Assert.Equal("string", schema.GetCSharpTypeName());
    }

    [Fact]
    public void GetCSharpTypeName_BooleanSchema_ReturnsBool()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Boolean };
        Assert.Equal("bool", schema.GetCSharpTypeName());
    }

    [Fact]
    public void GetCSharpTypeName_NumberSchema_ReturnsDouble()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Number };
        Assert.Equal("double", schema.GetCSharpTypeName());
    }

    [Fact]
    public void GetCSharpTypeName_NoType_ReturnsObject()
    {
        var schema = new OpenApiSchema();
        Assert.Equal("object", schema.GetCSharpTypeName());
    }

    // ========== Composition Tests ==========
    [Fact]
    public void HasAllOfComposition_WithAllOf_ReturnsTrue()
    {
        var schema = new OpenApiSchema
        {
            AllOf = [new OpenApiSchema { Type = JsonSchemaType.String }],
        };
        Assert.True(schema.HasAllOfComposition());
    }

    [Fact]
    public void HasAllOfComposition_NoAllOf_ReturnsFalse()
    {
        var schema = new OpenApiSchema();
        Assert.False(schema.HasAllOfComposition());
    }

    [Fact]
    public void HasOneOfComposition_WithOneOf_ReturnsTrue()
    {
        var schema = new OpenApiSchema
        {
            OneOf = [new OpenApiSchema { Type = JsonSchemaType.String }],
        };
        Assert.True(schema.HasOneOfComposition());
    }

    [Fact]
    public void HasOneOfComposition_NoOneOf_ReturnsFalse()
    {
        var schema = new OpenApiSchema();
        Assert.False(schema.HasOneOfComposition());
    }

    [Fact]
    public void HasAnyOfComposition_WithAnyOf_ReturnsTrue()
    {
        var schema = new OpenApiSchema
        {
            AnyOf = [new OpenApiSchema { Type = JsonSchemaType.String }],
        };
        Assert.True(schema.HasAnyOfComposition());
    }

    [Fact]
    public void HasPolymorphicComposition_WithOneOf_ReturnsTrue()
    {
        var schema = new OpenApiSchema
        {
            OneOf = [new OpenApiSchema { Type = JsonSchemaType.String }],
        };
        Assert.True(schema.HasPolymorphicComposition());
    }

    [Fact]
    public void HasPolymorphicComposition_NoComposition_ReturnsFalse()
    {
        var schema = new OpenApiSchema();
        Assert.False(schema.HasPolymorphicComposition());
    }

    // ========== HasConstValue / GetConstValue Tests ==========
    [Fact]
    public void HasConstValue_WithConst_ReturnsTrue()
    {
        var schema = new OpenApiSchema { Const = "active" };
        Assert.True(schema.HasConstValue());
    }

    [Fact]
    public void HasConstValue_NoConst_ReturnsFalse()
    {
        var schema = new OpenApiSchema();
        Assert.False(schema.HasConstValue());
    }

    [Fact]
    public void GetConstValue_WithConst_ReturnsValue()
    {
        var schema = new OpenApiSchema { Const = "active" };
        Assert.Equal("active", schema.GetConstValue());
    }

    [Fact]
    public void GetConstValue_NoConst_ReturnsNull()
    {
        var schema = new OpenApiSchema();
        Assert.Null(schema.GetConstValue());
    }

    // ========== HasUnevaluatedPropertiesRestriction Tests ==========
    [Fact]
    public void HasUnevaluatedPropertiesRestriction_WithAllOfAndFalse_ReturnsTrue()
    {
        var schema = new OpenApiSchema
        {
            AllOf = [new OpenApiSchema { Type = JsonSchemaType.Object }],
            UnevaluatedProperties = false,
        };
        Assert.True(schema.HasUnevaluatedPropertiesRestriction());
    }

    [Fact]
    public void HasUnevaluatedPropertiesRestriction_NoComposition_ReturnsFalse()
    {
        var schema = new OpenApiSchema { UnevaluatedProperties = false };
        Assert.False(schema.HasUnevaluatedPropertiesRestriction());
    }

    [Fact]
    public void HasUnevaluatedPropertiesRestriction_WithAllOfAndTrue_ReturnsFalse()
    {
        var schema = new OpenApiSchema
        {
            AllOf = [new OpenApiSchema { Type = JsonSchemaType.Object }],
            UnevaluatedProperties = true,
        };
        Assert.False(schema.HasUnevaluatedPropertiesRestriction());
    }

    // ========== ShouldMapToByteArray Tests ==========
    [Fact]
    public void ShouldMapToByteArray_FormatByte_ReturnsTrue()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "byte" };
        Assert.True(schema.ShouldMapToByteArray());
    }

    [Fact]
    public void ShouldMapToByteArray_NoByteFormat_ReturnsFalse()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        Assert.False(schema.ShouldMapToByteArray());
    }

    // ========== GetFileUploadInfo Tests ==========
    [Fact]
    public void GetFileUploadInfo_BinaryFormat_ReturnsSingleFile()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" };
        var (isFile, isCollection) = schema.GetFileUploadInfo();
        Assert.True(isFile);
        Assert.False(isCollection);
    }

    [Fact]
    public void GetFileUploadInfo_ArrayOfBinary_ReturnsFileCollection()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
        };
        var (isFile, isCollection) = schema.GetFileUploadInfo();
        Assert.True(isFile);
        Assert.True(isCollection);
    }

    [Fact]
    public void GetFileUploadInfo_RegularString_ReturnsNoFile()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        var (isFile, isCollection) = schema.GetFileUploadInfo();
        Assert.False(isFile);
        Assert.False(isCollection);
    }

    // ========== GetValidationAttributes Tests ==========
    [Fact]
    public void GetValidationAttributes_Required_IncludesRequired()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        var attrs = schema.GetValidationAttributes(isRequired: true);
        Assert.Contains("Required", attrs);
    }

    [Fact]
    public void GetValidationAttributes_NotRequired_NoRequired()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        var attrs = schema.GetValidationAttributes(isRequired: false);
        Assert.DoesNotContain("Required", attrs);
    }

    [Fact]
    public void GetValidationAttributes_IntegerRange_IncludesRange()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Integer,
            Minimum = "0",
            Maximum = "100",
        };
        var attrs = schema.GetValidationAttributes(isRequired: false);
        Assert.Contains("Range(0, 100)", attrs);
    }

    [Fact]
    public void GetValidationAttributes_StringMinMaxLength_IncludesBoth()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            MinLength = 1,
            MaxLength = 255,
        };
        var attrs = schema.GetValidationAttributes(isRequired: false);
        Assert.Contains("MinLength(1)", attrs);
        Assert.Contains("MaxLength(255)", attrs);
    }

    [Fact]
    public void GetValidationAttributes_StringPattern_IncludesRegex()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Pattern = "^[a-z]+$",
        };
        var attrs = schema.GetValidationAttributes(isRequired: false);
        Assert.Single(attrs, a => a.Contains("RegularExpression", StringComparison.Ordinal));
    }

    [Fact]
    public void GetValidationAttributes_EmailFormat_IncludesEmailAddress()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "email",
        };
        var attrs = schema.GetValidationAttributes(isRequired: false);
        Assert.Contains("EmailAddress", attrs);
    }

    [Fact]
    public void GetValidationAttributes_UriFormat_NoLengthAttributes()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "uri",
            MinLength = 5,
        };
        var attrs = schema.GetValidationAttributes(isRequired: false);
        Assert.DoesNotContain(attrs, a => a.Contains("MinLength", StringComparison.Ordinal));
    }

    [Fact]
    public void GetValidationAttributes_ArrayMinItems_IncludesMinLength()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            MinItems = 1,
        };
        var attrs = schema.GetValidationAttributes(isRequired: false);
        Assert.Contains("MinLength(1)", attrs);
    }

    // ========== ToCSharpType (Parameter) Tests ==========
    [Fact]
    public void ToCSharpType_RequiredString_ReturnsString()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        Assert.Equal("string", schema.ToCSharpType(isRequired: true));
    }

    [Fact]
    public void ToCSharpType_OptionalString_ReturnsNullableString()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        Assert.Equal("string?", schema.ToCSharpType(isRequired: false));
    }

    [Fact]
    public void ToCSharpType_RequiredInteger_ReturnsInt()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
        Assert.Equal("int", schema.ToCSharpType(isRequired: true));
    }

    [Fact]
    public void ToCSharpType_OptionalInteger_ReturnsNullableInt()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
        Assert.Equal("int?", schema.ToCSharpType(isRequired: false));
    }

    [Fact]
    public void ToCSharpType_RequiredBoolean_ReturnsBool()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Boolean };
        Assert.Equal("bool", schema.ToCSharpType(isRequired: true));
    }

    [Fact]
    public void ToCSharpType_OptionalBoolean_ReturnsNullableBool()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Boolean };
        Assert.Equal("bool?", schema.ToCSharpType(isRequired: false));
    }

    [Fact]
    public void ToCSharpType_UuidFormat_ReturnsGuid()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" };
        Assert.Equal("Guid", schema.ToCSharpType(isRequired: true));
    }

    [Fact]
    public void ToCSharpType_OptionalGuid_ReturnsNullableGuid()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" };
        Assert.Equal("Guid?", schema.ToCSharpType(isRequired: false));
    }

    [Fact]
    public void ToCSharpType_DateTimeFormat_ReturnsDateTimeOffset()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "date-time" };
        Assert.Equal("DateTimeOffset", schema.ToCSharpType(isRequired: true));
    }

    [Fact]
    public void ToCSharpType_ByteFormat_ReturnsByteArray()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "byte" };
        Assert.Equal("byte[]", schema.ToCSharpType(isRequired: true));
    }

    [Fact]
    public void ToCSharpType_ArrayOfStrings_ReturnsList()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { Type = JsonSchemaType.String },
        };
        Assert.Equal("List<string>", schema.ToCSharpType(isRequired: true));
    }

    [Fact]
    public void ToCSharpType_OptionalArray_ReturnsNullableList()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { Type = JsonSchemaType.Integer },
        };
        Assert.Equal("List<int>?", schema.ToCSharpType(isRequired: false));
    }

    // ========== ToCSharpTypeForModel Primitives ==========
    [Fact]
    public void ToCSharpTypeForModel_NullableString_ReturnsNullableString()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null };
        Assert.Equal("string?", schema.ToCSharpTypeForModel(isRequired: false));
    }

    [Fact]
    public void ToCSharpTypeForModel_NonNullableString_ReturnsString()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        Assert.Equal("string", schema.ToCSharpTypeForModel(isRequired: false));
    }

    [Fact]
    public void ToCSharpTypeForModel_NullableInteger_ReturnsNullableInt()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null };
        Assert.Equal("int?", schema.ToCSharpTypeForModel(isRequired: false));
    }

    [Fact]
    public void ToCSharpTypeForModel_NonNullableInteger_ReturnsInt()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
        Assert.Equal("int", schema.ToCSharpTypeForModel(isRequired: true));
    }

    [Fact]
    public void ToCSharpTypeForModel_ByteFormat_ReturnsByteArray()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "byte" };
        Assert.Equal("byte[]", schema.ToCSharpTypeForModel(isRequired: true));
    }

    [Fact]
    public void ToCSharpTypeForModel_NullableByteArray_ReturnsNullable()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "byte" };
        Assert.Equal("byte[]?", schema.ToCSharpTypeForModel(isRequired: false));
    }

    // ========== AdditionalProperties / Dictionary Tests ==========
    [Fact]
    public void HasAdditionalProperties_WithTypedProps_ReturnsTrue()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String },
        };
        Assert.True(schema.HasAdditionalProperties());
    }

    [Fact]
    public void HasAdditionalProperties_NoProps_ReturnsFalse()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };
        Assert.False(schema.HasAdditionalProperties());
    }

    [Fact]
    public void GetDictionaryTypeString_WithStringValues_ReturnsDictionaryString()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String },
        };
        var result = schema.GetDictionaryTypeString(isRequired: true);
        Assert.Equal("Dictionary<string, string>", result);
    }

    [Fact]
    public void GetDictionaryTypeString_NoAdditionalProps_ReturnsNull()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };
        Assert.Null(schema.GetDictionaryTypeString(isRequired: true));
    }

    // ========== GetDiscriminatorPropertyName Tests ==========
    [Fact]
    public void GetDiscriminatorPropertyName_WithDiscriminator_ReturnsName()
    {
        var schema = new OpenApiSchema
        {
            Discriminator = new OpenApiDiscriminator { PropertyName = "type" },
        };
        Assert.Equal("type", schema.GetDiscriminatorPropertyName());
    }

    [Fact]
    public void GetDiscriminatorPropertyName_NoDiscriminator_ReturnsNull()
    {
        var schema = new OpenApiSchema();
        Assert.Null(schema.GetDiscriminatorPropertyName());
    }

    // ========== GetAllOfBaseSchemaName Tests (via YAML) ==========
    [Fact]
    public void GetAllOfBaseSchemaName_WithBaseRef_ReturnsBaseName()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Dog:
                                  allOf:
                                    - $ref: '#/components/schemas/Animal'
                                    - type: object
                                      properties:
                                        breed:
                                          type: string
                                Animal:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var dogSchema = document!.Components!.Schemas["Dog"];
        Assert.Equal("Animal", dogSchema.GetAllOfBaseSchemaName());
    }

    [Fact]
    public void GetAllOfBaseSchemaName_NoAllOf_ReturnsNull()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };
        Assert.Null(schema.GetAllOfBaseSchemaName());
    }

    // ========== GetImplementedInterfaces Tests (via YAML) ==========
    [Fact]
    public void GetImplementedInterfaces_WithExtension_ReturnsInterfaces()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  x-implements:
                                    - IEntity
                                    - IAuditable
                                  properties:
                                    id:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var petSchema = document!.Components!.Schemas["Pet"];
        var interfaces = petSchema.GetImplementedInterfaces();

        Assert.Contains("IEntity", interfaces);
        Assert.Contains("IAuditable", interfaces);
    }

    [Fact]
    public void GetImplementedInterfaces_NoExtension_ReturnsEmpty()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };
        var interfaces = schema.GetImplementedInterfaces();
        Assert.Empty(interfaces);
    }

    // ========== GetReferenceId Tests (via YAML) ==========
    [Fact]
    public void GetReferenceId_SchemaReference_ReturnsId()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                Parent:
                                  type: object
                                  properties:
                                    child:
                                      $ref: '#/components/schemas/Child'
                                Child:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var parentSchema = document!.Components!.Schemas["Parent"] as OpenApiSchema;
        Assert.NotNull(parentSchema);

        var childProperty = parentSchema!.Properties["child"];
        Assert.Equal("Child", childProperty.GetReferenceId());
    }

    [Fact]
    public void GetReferenceId_NonReference_ReturnsNull()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        Assert.Null(schema.GetReferenceId());
    }

    // ========== ResolveSchema Tests ==========
    [Fact]
    public void ResolveSchema_DirectSchema_ReturnsSelf()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "Test", Version = "1.0" },
        };
        var resolved = schema.ResolveSchema(document);
        Assert.Same(schema, resolved);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}