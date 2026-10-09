namespace Atc.OpenApi.Tests.Extensions;

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

        var deviceSchema = document.Components.Schemas["Device"] as OpenApiSchema;
        Assert.NotNull(deviceSchema);

        var customerProperty = deviceSchema.Properties["customer"];
        Assert.NotNull(customerProperty);

        // Act
        var typeName = customerProperty.ToCSharpTypeForModel(isRequired: true);

        // Assert
        // Spec note: `nullable: true` only applies when `type` is in the same Schema Object,
        // and never reaches through allOf/oneOf (OAI "Clarify Semantics of nullable", 2019).
        // Microsoft.OpenApi 3.7.0 honoured it here anyway; 3.10.2 correctly does not.
        Assert.Equal("IdValue", typeName);
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

        var deviceSchema = document.Components.Schemas["Device"] as OpenApiSchema;
        var ownerProperty = deviceSchema.Properties["owner"];

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

        var vehicleSchema = document.Components.Schemas["Vehicle"] as OpenApiSchema;
        var ownerProperty = vehicleSchema.Properties["owner"];

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

        var deviceSchema = document.Components.Schemas["Device"] as OpenApiSchema;
        var settingsProperty = deviceSchema.Properties["settings"];

        // Act
        var typeName = settingsProperty.ToCSharpTypeForModel(isRequired: true);

        // Assert
        // See the spec note above: nullable does not reach through allOf.
        Assert.Equal("DeviceSettings", typeName);
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

        var deviceSchema = document.Components.Schemas["Device"] as OpenApiSchema;
        var deviceTypeProperty = deviceSchema.Properties["deviceType"];

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

        var deviceSchema = document.Components.Schemas["Device"] as OpenApiSchema;
        var suggestedTypeProperty = deviceSchema.Properties["suggestedType"];

        // Act
        var typeName = suggestedTypeProperty.ToCSharpTypeForModel(isRequired: true);

        // Assert
        // See the spec note above: nullable does not reach through oneOf.
        Assert.Equal("DeviceType", typeName);
    }

    // ========== SanitizeSchemaName Tests ==========
    [Theory]
    [InlineData("Pet", "Pet")]
    [InlineData("Foo.Bar.Baz", "Foo_Bar_Baz")]
    [InlineData("Simple", "Simple")]
    [InlineData("A.B", "A_B")]
    [InlineData("", "")]
    [InlineData("3DModel", "_3DModel")]
    [InlineData("Page«User»", "Page_User_")]
    [InlineData("class", "Class")] // a C# keyword cannot be a type name: the first letter is capitalised
    [InlineData("object", "Object")]
    [InlineData("string", "String")]
    [InlineData("__arglist", "___arglist")] // a keyword with no letter to capitalise gets an underscore
    [InlineData("__makeref", "___makeref")]
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
        var result = OpenApiSchemaExtensions.SanitizeSchemaName(null);
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

    /// <summary>
    /// An integer whose bounds do not fit an <c>int</c> (a Unix timestamp in milliseconds, an unsigned 32-bit id) is
    /// a <c>long</c>, for a model, a parameter and a type name.
    /// </summary>
    [Theory]
    [InlineData("4294967295", null, "long")]
    [InlineData(null, "-2147483649", "long")]
    [InlineData("2147483647", "-2147483648", "int")]
    [InlineData("100", null, "int")]
    public void AnIntegerWithBoundsPastInt32_IsALong(
        string? maximum,
        string? minimum,
        string expected)
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Maximum = maximum, Minimum = minimum };

        Assert.Equal(expected, schema.GetCSharpTypeName());
        Assert.Equal(expected, schema.ToCSharpType(isRequired: true));
        Assert.Equal(expected, schema.ToCSharpTypeForModel(isRequired: true));
    }

    /// <summary>
    /// A <c>const</c> without a <c>type</c> (JSON Schema 2020-12 needs none) is the type of its value: the reader
    /// keeps a const as text, so it is a string - not <c>object</c>, which no client could pass the value to.
    /// </summary>
    [Fact]
    public void AConstWithoutAType_IsAString_ForAModelAParameterAndAName()
    {
        var schema = new OpenApiSchema { Const = "fixed" };

        Assert.Equal("string", schema.GetCSharpTypeName());
        Assert.Equal("string", schema.ToCSharpType(isRequired: true));
        Assert.Equal("string?", schema.ToCSharpType(isRequired: false));
        Assert.Equal("string", schema.ToCSharpTypeForModel(isRequired: true));
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
        Assert.Contains("Required", attrs, StringComparer.Ordinal);
    }

    [Fact]
    public void GetValidationAttributes_NotRequired_NoRequired()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };
        var attrs = schema.GetValidationAttributes(isRequired: false);
        Assert.DoesNotContain("Required", attrs, StringComparer.Ordinal);
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
        Assert.Contains("Range(0, 100)", attrs, StringComparer.Ordinal);
    }

    [Fact]
    public void GetValidationAttributes_StringMinMaxLength_IncludesBothApiLengths()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            MinLength = 1,
            MaxLength = 255,
        };
        var attrs = schema.GetValidationAttributes(isRequired: false);
        Assert.Contains("ApiMinLength(1)", attrs, StringComparer.Ordinal);
        Assert.Contains("ApiMaxLength(255)", attrs, StringComparer.Ordinal);
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
        Assert.Contains("EmailAddress", attrs, StringComparer.Ordinal);
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
    public void GetValidationAttributes_ArrayMinItems_IncludesApiMinLength()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            MinItems = 1,
        };
        var attrs = schema.GetValidationAttributes(isRequired: false);
        Assert.Contains("ApiMinLength(1)", attrs, StringComparer.Ordinal);
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
        Assert.Equal("string", schema.ToCSharpTypeForModel(isRequired: true));
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

    // An optional property is nullable: a property left out of the JSON is null - for a value type not a default (0,
    // false) that looks like a sent value, for a reference type not a "never null" the compiler trusts.
    [Theory]
    [InlineData(JsonSchemaType.Integer, null, "int?")]
    [InlineData(JsonSchemaType.Integer, "int64", "long?")]
    [InlineData(JsonSchemaType.Number, "double", "double?")]
    [InlineData(JsonSchemaType.Boolean, null, "bool?")]
    [InlineData(JsonSchemaType.String, "uuid", "Guid?")]
    [InlineData(JsonSchemaType.String, "date-time", "DateTimeOffset?")]
    [InlineData(JsonSchemaType.String, null, "string?")]
    [InlineData(JsonSchemaType.String, "uri", "Uri?")]
    [InlineData(JsonSchemaType.String, "byte", "byte[]?")]
    public void ToCSharpTypeForModel_OptionalProperty_IsNullable(
        JsonSchemaType type,
        string? format,
        string expected)
    {
        var schema = new OpenApiSchema { Type = type, Format = format };
        Assert.Equal(expected, schema.ToCSharpTypeForModel(isRequired: false));
        Assert.Equal(expected.TrimEnd('?'), schema.ToCSharpTypeForModel(isRequired: true));
    }

    // An optional property with a default is not nullable: a property left out of the JSON is the default.
    [Fact]
    public void ToCSharpTypeForModel_OptionalPropertyWithDefault_IsNotNullable()
    {
        var flag = new OpenApiSchema { Type = JsonSchemaType.Boolean, Default = System.Text.Json.Nodes.JsonValue.Create(true) };
        Assert.Equal("bool", flag.ToCSharpTypeForModel(isRequired: false));

        var text = new OpenApiSchema { Type = JsonSchemaType.String, Default = System.Text.Json.Nodes.JsonValue.Create("none") };
        Assert.Equal("string", text.ToCSharpTypeForModel(isRequired: false));
    }

    [Fact]
    public void ToCSharpTypeForModel_OptionalArray_IsNullable()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } };
        Assert.Equal("List<string>?", schema.ToCSharpTypeForModel(isRequired: false));
        Assert.Equal("List<string>", schema.ToCSharpTypeForModel(isRequired: true));
    }

    [Fact]
    public void ToCSharpTypeForModel_OptionalReference_IsNullable()
    {
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
                                    owner:
                                      $ref: '#/components/schemas/Owner'
                                    settings:
                                      allOf:
                                        - $ref: '#/components/schemas/Owner'
                                    holder:
                                      oneOf:
                                        - $ref: '#/components/schemas/Owner'
                                DeviceType:
                                  type: string
                                  enum:
                                    - TypeA
                                    - TypeB
                                Owner:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);
        var deviceSchema = document.Components.Schemas["Device"] as OpenApiSchema;

        // An enum is a value type: optional means nullable, as for an inline enum.
        Assert.Equal("DeviceType?", deviceSchema!.Properties["deviceType"].ToCSharpTypeForModel(isRequired: false));
        Assert.Equal("DeviceType", deviceSchema.Properties["deviceType"].ToCSharpTypeForModel(isRequired: true));

        // A model reference - direct, through allOf or through a single oneOf: optional means nullable too.
        foreach (var name in (string[])["owner", "settings", "holder"])
        {
            Assert.Equal("Owner?", deviceSchema.Properties[name].ToCSharpTypeForModel(isRequired: false));
            Assert.Equal("Owner", deviceSchema.Properties[name].ToCSharpTypeForModel(isRequired: true));
        }
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

        var dogSchema = document.Components.Schemas["Dog"];
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

        var petSchema = document.Components.Schemas["Pet"];
        var interfaces = petSchema.GetImplementedInterfaces();

        Assert.Contains("IEntity", interfaces, StringComparer.Ordinal);
        Assert.Contains("IAuditable", interfaces, StringComparer.Ordinal);
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

        var parentSchema = document.Components.Schemas["Parent"] as OpenApiSchema;
        Assert.NotNull(parentSchema);

        var childProperty = parentSchema.Properties["child"];
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

    // ========== GetPaginationAnnotation Tests ==========
    [Fact]
    public void GetPaginationAnnotation_WithXPaginationTrue_ReturnsTrue()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                PagedResponse:
                                  type: object
                                  x-pagination: true
                                  properties:
                                    entries:
                                      type: array
                                      items:
                                        type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schema = document.Components.Schemas["PagedResponse"];

        // Act
        var result = schema.GetPaginationAnnotation();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetPaginationAnnotation_WithXPaginationFalse_ReturnsFalse()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                SearchResults:
                                  type: object
                                  x-pagination: false
                                  properties:
                                    results:
                                      type: array
                                      items:
                                        type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schema = document.Components.Schemas["SearchResults"];

        // Act
        var result = schema.GetPaginationAnnotation();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetPaginationAnnotation_WithoutAnnotation_ReturnsNull()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                SimpleModel:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schema = document.Components.Schemas["SimpleModel"];

        // Act
        var result = schema.GetPaginationAnnotation();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetPaginationAnnotation_WithNonBooleanValue_ReturnsNull()
    {
        // Arrange - guards the non-reflection path against unexpected extension payloads.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths: {}
                            components:
                              schemas:
                                StringAnnotated:
                                  type: object
                                  x-pagination: "yes"
                                  properties:
                                    name:
                                      type: string
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var schema = document.Components.Schemas["StringAnnotated"];

        // Act
        var result = schema.GetPaginationAnnotation();

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Both spellings of "this property may be null" produce a nullable C# type, at every spec
    /// version — the OpenAPI 3.0 <c>nullable: true</c> keyword and the 3.1 <c>type: [X, "null"]</c>
    /// array.
    /// </summary>
    /// <remarks>
    /// Supporting both is a deliberate compatibility choice rather than a reading of the spec:
    /// 3.1 removed <c>nullable</c>, so writing it in a 3.1 document is invalid. Rejecting it would
    /// silently flip a consumer's model from <c>string?</c> to <c>string</c>, which fails at
    /// runtime rather than at build time, so the keyword is honoured wherever it appears.
    /// </remarks>
    [Theory]
    [InlineData("3.0.4")]
    [InlineData("3.1.0")]
    [InlineData("3.2.0")]
    public void ToCSharpTypeForModel_NullableKeywordSpelling_ProducesANullableType(
        string specVersion)
    {
        var yaml = $$"""
                     openapi: "{{specVersion}}"
                     info:
                       title: T
                       version: "1.0.0"
                     paths: {}
                     components:
                       schemas:
                         Person:
                           type: object
                           title: Person
                           required:
                             - nickname
                           properties:
                             nickname:
                               type: string
                               nullable: true
                     """;

        AssertNicknameIsNullable(yaml);
    }

    /// <summary>
    /// The 3.1 spelling. Type arrays are a JSON Schema feature that OpenAPI 3.0 does not have, so
    /// this form is only meaningful from 3.1 onward.
    /// </summary>
    [Theory]
    [InlineData("3.1.0")]
    [InlineData("3.2.0")]
    public void ToCSharpTypeForModel_TypeArraySpelling_ProducesANullableType(
        string specVersion)
    {
        var yaml = $$"""
                     openapi: "{{specVersion}}"
                     info:
                       title: T
                       version: "1.0.0"
                     paths: {}
                     components:
                       schemas:
                         Person:
                           type: object
                           title: Person
                           required:
                             - nickname
                           properties:
                             nickname:
                               type: [string, "null"]
                     """;

        AssertNicknameIsNullable(yaml);
    }

    /// <summary>
    /// A nullable <c>$ref</c> written the 3.0 way — <c>nullable: true</c> beside an
    /// <c>allOf</c> — inside a 3.1+ document.
    /// </summary>
    /// <remarks>
    /// The keyword is invalid in 3.1, so the reader parks it in <c>UnrecognizedKeywords</c> rather
    /// than dropping it, which leaves enough for the generator to honour the author's intent.
    /// <para>
    /// The same shape in a <b>3.0</b> document cannot be supported: there the keyword is
    /// recognised, consumed, and then discarded because there is no <c>type</c> in that Schema
    /// Object to modify, so nothing reaches the generator. That case has its own test asserting
    /// the non-nullable result.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("3.1.0")]
    [InlineData("3.2.0")]
    public void ToCSharpTypeForModel_NullableKeywordBesideAllOf_IsHonouredInThreeOneAndLater(
        string specVersion)
    {
        var yaml = $$"""
                     openapi: "{{specVersion}}"
                     info:
                       title: T
                       version: "1.0.0"
                     paths: {}
                     components:
                       schemas:
                         Person:
                           type: object
                           title: Person
                           properties:
                             address:
                               nullable: true
                               allOf:
                                 - $ref: '#/components/schemas/Address'
                         Address:
                           type: object
                           title: Address
                           properties:
                             street:
                               type: string
                     """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var person = (OpenApiSchema)document.Components!.Schemas!["Person"];
        var address = person.Properties!["address"];

        Assert.Equal("Address?", address.ToCSharpTypeForModel(isRequired: true, registry: null));
    }

    [Fact]
    public void ToCSharpTypeForModel_OneOfRefPlusNullBranch_IsTheReferencedTypeMadeNullable()
    {
        // The idiomatic OpenAPI 3.1 nullable-$ref: `oneOf: [$ref, type: "null"]`. It replaces
        // 3.0's `nullable: true` + `allOf`, which the spec says never worked. Without this the
        // second branch defeats the single-ref shortcut and the property degrades to `object`.
        const string yaml = """
                            openapi: "3.1.0"
                            info:
                              title: T
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Person:
                                  type: object
                                  title: Person
                                  properties:
                                    address:
                                      oneOf:
                                        - $ref: '#/components/schemas/Address'
                                        - type: "null"
                                Address:
                                  type: object
                                  title: Address
                                  properties:
                                    street:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var person = (OpenApiSchema)document.Components!.Schemas!["Person"];
        var address = person.Properties!["address"];

        Assert.Equal("Address?", address.ToCSharpTypeForModel(isRequired: true, registry: null));
    }

    [Fact]
    public void ToCSharpTypeForModel_OneOfRefPlusNullableObjectBranch_IsTheReferencedTypeMadeNullable()
    {
        // The OpenAPI 3.0 nullable-$ref. 3.0 has no `type: "null"`, so the null branch is spelled as
        // an object that is nullable, and the nullability has to live inside the composition because
        // a `nullable` sibling of `oneOf` has no `type` to apply to - which is what ATC_API_VER001
        // reports.
        //
        // This is the rewrite the VER001 diagnostic suggests and the one NexusSample uses, so it has
        // to produce `Address?` rather than degrade to `object` on the extra branch.
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: T
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Person:
                                  type: object
                                  title: Person
                                  properties:
                                    address:
                                      oneOf:
                                        - $ref: '#/components/schemas/Address'
                                        - type: object
                                          nullable: true
                                Address:
                                  type: object
                                  title: Address
                                  properties:
                                    street:
                                      type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var person = (OpenApiSchema)document.Components!.Schemas!["Person"];
        var address = person.Properties!["address"];

        Assert.Equal("Address?", address.ToCSharpTypeForModel(isRequired: true, registry: null));
    }

    [Theory]
    [InlineData("3.0.4")]
    [InlineData("3.1.0")]
    [InlineData("3.2.0")]
    public void IsNullable_HonoursTheNullableKeyword_AtEverySpecVersion(
        string specVersion)
    {
        // `nullable: true` is an OpenAPI 3.0 keyword. Microsoft.OpenApi 3.7.0 still honoured it in
        // a 3.1 document; 3.10.2 correctly ignores it, parking it in UnrecognizedKeywords instead.
        //
        // Strictly the 3.1 spec is on the library's side, but silently flipping a consumer's
        // generated model from `string?` to `string` on a package bump is a breaking change they
        // never asked for and would not notice until it failed at runtime. The keyword is still
        // honoured — and ATC_API_SCH022 tells the author to migrate to `type: [x, 'null']`.
        var yaml = $$"""
                     openapi: "{{specVersion}}"
                     info:
                       title: T
                       version: "1.0.0"
                     paths: {}
                     components:
                       schemas:
                         Address:
                           type: object
                           title: Address
                           required:
                             - street
                           properties:
                             street:
                               type: string
                             state:
                               type: string
                               nullable: true
                     """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var address = (OpenApiSchema)document.Components!.Schemas!["Address"];

        Assert.False(((OpenApiSchema)address.Properties!["street"]).IsNullable());
        Assert.True(((OpenApiSchema)address.Properties!["state"]).IsNullable());
    }

    // ========== IsSchemaReference / IsOfType Tests ==========
    [Fact]
    public void IsSchemaReference_ReferenceAndInline_AreToldApart()
    {
        var doc = ParsePolymorphismDocument();

        Assert.True(doc.Components!.Schemas!["Animal"].OneOf![0].IsSchemaReference());
        Assert.False(doc.Components.Schemas["Pet"].IsSchemaReference());
    }

    [Theory]
    [InlineData(JsonSchemaType.Object, JsonSchemaType.Object, true)]
    [InlineData(JsonSchemaType.Object | JsonSchemaType.Null, JsonSchemaType.Object, true)]
    [InlineData(JsonSchemaType.Array | JsonSchemaType.Null, JsonSchemaType.Array, true)]
    [InlineData(JsonSchemaType.String, JsonSchemaType.Object, false)]
    [InlineData(JsonSchemaType.String | JsonSchemaType.Integer, JsonSchemaType.String, false)]
    public void IsOfType_IgnoresTheNullFlagOnly(
        JsonSchemaType declared,
        JsonSchemaType wanted,
        bool expected)
        => Assert.Equal(expected, new OpenApiSchema { Type = declared }.IsOfType(wanted));

    [Fact]
    public void IsOfType_NoTypeOrReference_ReturnsFalse()
    {
        var doc = ParsePolymorphismDocument();

        Assert.False(new OpenApiSchema().IsOfType(JsonSchemaType.Object));
        Assert.False(doc.Components!.Schemas!["Animal"].OneOf![0].IsOfType(JsonSchemaType.Object));
    }

    // ========== oneOf branch inspection Tests ==========
    [Fact]
    public void HasNullBranchInOneOf_TypeNullBranch_ReturnsTrue()
    {
        var doc = ParsePolymorphismDocument();

        Assert.True(doc.Components!.Schemas!["NullablePet"].HasNullBranchInOneOf());
    }

    [Fact]
    public void HasNullBranchInOneOf_OnlyReferences_ReturnsFalse()
    {
        var doc = ParsePolymorphismDocument();

        Assert.False(doc.Components!.Schemas!["Animal"].HasNullBranchInOneOf());
        Assert.False(doc.Components.Schemas["Pet"].HasNullBranchInOneOf());
    }

    [Fact]
    public void HasNonReferenceBranchInOneOf_InlineStringBranch_ReturnsTrue()
    {
        var doc = ParsePolymorphismDocument();

        Assert.True(doc.Components!.Schemas!["PetOrName"].HasNonReferenceBranchInOneOf());
    }

    [Fact]
    public void HasNonReferenceBranchInOneOf_ReferencesAndNullMarker_ReturnsFalse()
    {
        var doc = ParsePolymorphismDocument();

        Assert.False(doc.Components!.Schemas!["Animal"].HasNonReferenceBranchInOneOf());
        Assert.False(doc.Components.Schemas["NullablePet"].HasNonReferenceBranchInOneOf());
    }

    [Fact]
    public void GetOneOfReferenceIds_OneOf_ReturnsReferencedNamesInOrderWithoutTheNullBranch()
    {
        var doc = ParsePolymorphismDocument();

        Assert.Equal(["Cat", "Dog"], doc.Components!.Schemas!["Animal"].GetOneOfReferenceIds());
        Assert.Equal(["Pet"], doc.Components.Schemas["NullablePet"].GetOneOfReferenceIds());
    }

    [Fact]
    public void GetOneOfReferenceIds_AnyOfOnly_FallsBackToAnyOf()
    {
        var doc = ParsePolymorphismDocument();

        Assert.Equal(["Cat", "Dog"], doc.Components!.Schemas!["AnyAnimal"].GetOneOfReferenceIds());
    }

    [Fact]
    public void GetOneOfReferenceIds_NoComposition_ReturnsEmpty()
        => Assert.Empty(new OpenApiSchema { Type = JsonSchemaType.String }.GetOneOfReferenceIds());

    // ========== Type array Tests ==========
    [Fact]
    public void GetAllNonNullTypeNames_StringOrIntegerOrNull_ListsBothWithoutNull()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Integer | JsonSchemaType.Null };

        Assert.Equal(["string", "integer"], schema.GetAllNonNullTypeNames());
    }

    [Fact]
    public void GetAllNonNullTypeNames_NoType_ReturnsEmpty()
        => Assert.Empty(new OpenApiSchema().GetAllNonNullTypeNames());

    [Fact]
    public void GetPrimaryNonNullType_IntegerOrNumber_PrefersInteger()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.Integer | JsonSchemaType.Null };

        Assert.Equal(JsonSchemaType.Integer, schema.GetPrimaryNonNullType());
    }

    [Fact]
    public void GetPrimaryNonNullType_NoType_ReturnsNull()
        => Assert.Null(new OpenApiSchema().GetPrimaryNonNullType());

    // ========== GetSchemaSummary / HasRefSiblingProperties Tests ==========
    [Fact]
    public void GetSchemaSummary_Declared_ReturnsIt()
    {
        var doc = ParseYaml("""
                            openapi: "3.2.0"
                            info:
                              title: T
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  summary: A pet of the store
                                  description: Longer text.
                            """);
        Assert.NotNull(doc);

        Assert.Equal("A pet of the store", doc.Components!.Schemas!["Pet"].GetSchemaSummary());
    }

    [Fact]
    public void GetSchemaSummary_NotDeclared_ReturnsNull()
    {
        var doc = ParsePolymorphismDocument();

        Assert.Null(doc.Components!.Schemas!["Pet"].GetSchemaSummary());
        Assert.Null(doc.Components.Schemas["Animal"].OneOf![0].GetSchemaSummary());
    }

    [Fact]
    public void HasRefSiblingProperties_ReferenceWithDescription_ReturnsTrue()
    {
        var doc = ParseYaml("""
                            openapi: "3.1.0"
                            info:
                              title: T
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                Owner:
                                  type: object
                                  properties:
                                    favourite:
                                      $ref: '#/components/schemas/Pet'
                                      description: The owner's favourite pet
                                    other:
                                      $ref: '#/components/schemas/Pet'
                            """);
        Assert.NotNull(doc);

        var owner = doc.Components!.Schemas!["Owner"];

        Assert.True(owner.Properties!["favourite"].HasRefSiblingProperties());
        Assert.False(owner.Properties["other"].HasRefSiblingProperties());
    }

    [Fact]
    public void HasRefSiblingProperties_InlineSchema_ReturnsFalse()
        => Assert.False(new OpenApiSchema { Description = "x", Deprecated = true }.HasRefSiblingProperties());

    // ========== contentEncoding Tests ==========
    [Theory]
    [InlineData("base64", true)]
    [InlineData("BASE64URL", true)]
    [InlineData("quoted-printable", false)]
    public void GetContentEncoding_And_IsBase64Encoded_ReadTheUnrecognizedKeyword(
        string contentEncoding,
        bool expectedBase64)
    {
        // The helper reads the keyword from UnrecognizedKeywords, where a reader that does not know
        // contentEncoding parks it.
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            UnrecognizedKeywords = new Dictionary<string, JsonNode>(StringComparer.Ordinal)
            {
                ["contentEncoding"] = JsonValue.Create(contentEncoding)!,
            },
        };

        Assert.Equal(contentEncoding, schema.GetContentEncoding());
        Assert.Equal(expectedBase64, schema.IsBase64Encoded());
    }

    [Fact]
    public void GetContentEncoding_NotDeclared_ReturnsNullAndNotBase64()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };

        Assert.Null(schema.GetContentEncoding());
        Assert.False(schema.IsBase64Encoded());
    }

    // ========== prefixItems Tests ==========
    [Fact]
    public void HasPrefixItems_And_IsStrictTuple_NoItemsKeyword_IsAClosedTuple()
    {
        var schema = ParseTupleSchema(string.Empty);

        Assert.True(schema.HasPrefixItems());
        Assert.True(schema.IsStrictTuple());
    }

    [Fact]
    public void IsStrictTuple_OpenTuple_ReturnsFalse()
    {
        var schema = ParseTupleSchema("items:\n        type: string");

        Assert.True(schema.HasPrefixItems());
        Assert.False(schema.IsStrictTuple());
    }

    [Fact]
    public void HasPrefixItems_PlainArray_ReturnsFalseAndNotStrict()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } };

        Assert.False(schema.HasPrefixItems());
        Assert.False(schema.IsStrictTuple());
    }

    // ========== Discriminator Tests ==========
    [Fact]
    public void HasDiscriminatorBlock_DeclaredAndNot_AreToldApart()
    {
        var doc = ParsePolymorphismDocument();

        Assert.True(doc.Components!.Schemas!["Animal"].HasDiscriminatorBlock());
        Assert.False(doc.Components.Schemas["AnyAnimal"].HasDiscriminatorBlock());
    }

    [Fact]
    public void GetDiscriminatorMapping_Declared_ReturnsValueToSchemaName()
    {
        var doc = ParsePolymorphismDocument();

        var mapping = doc.Components!.Schemas!["Animal"].GetDiscriminatorMapping();

        Assert.NotNull(mapping);
        Assert.Equal("Cat", mapping["cat"]);
        Assert.Equal("Dog", mapping["dog"]);
        Assert.Equal(2, mapping.Count);
    }

    [Fact]
    public void GetDiscriminatorMapping_NoMapping_ReturnsNull()
    {
        var doc = ParsePolymorphismDocument();

        Assert.Null(doc.Components!.Schemas!["AnyAnimal"].GetDiscriminatorMapping());
    }

    [Fact]
    public void GetDiscriminatorDefaultMappingSchemaName_OpenApi32DefaultMapping_ReturnsSchemaName()
    {
        var doc = ParseYaml("""
                            openapi: "3.2.0"
                            info:
                              title: T
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Order:
                                  oneOf:
                                    - $ref: '#/components/schemas/OnlineOrder'
                                    - $ref: '#/components/schemas/InStoreOrder'
                                  discriminator:
                                    propertyName: orderType
                                    mapping:
                                      online: '#/components/schemas/OnlineOrder'
                                      in_store: '#/components/schemas/InStoreOrder'
                                    defaultMapping: '#/components/schemas/OnlineOrder'
                                OnlineOrder:
                                  type: object
                                  properties:
                                    orderType:
                                      type: string
                                InStoreOrder:
                                  type: object
                                  properties:
                                    orderType:
                                      type: string
                            """);
        Assert.NotNull(doc);

        Assert.Equal("OnlineOrder", doc.Components!.Schemas!["Order"].GetDiscriminatorDefaultMappingSchemaName());
    }

    [Fact]
    public void GetDiscriminatorDefaultMappingSchemaName_NoDefaultMapping_ReturnsNull()
    {
        var doc = ParsePolymorphismDocument();

        Assert.Null(doc.Components!.Schemas!["Animal"].GetDiscriminatorDefaultMappingSchemaName());
    }

    [Fact]
    public void GetPolymorphicVariantSchemaNames_OneOfAndAnyOf_ReturnReferencedNames()
    {
        var doc = ParsePolymorphismDocument();

        Assert.Equal(["Cat", "Dog"], doc.Components!.Schemas!["Animal"].GetPolymorphicVariantSchemaNames());
        Assert.Equal(["Cat", "Dog"], doc.Components.Schemas["AnyAnimal"].GetPolymorphicVariantSchemaNames());
        Assert.Equal(["Pet"], doc.Components.Schemas["PetOrName"].GetPolymorphicVariantSchemaNames());
        Assert.Empty(doc.Components.Schemas["Pet"].GetPolymorphicVariantSchemaNames());
    }

    [Fact]
    public void DetectDiscriminatorProperty_CommonStringPropertyNamedType_ReturnsItWithDeclaredCasing()
    {
        var doc = ParsePolymorphismDocument();

        Assert.Equal("type", doc.Components!.Schemas!["AnyAnimal"].DetectDiscriminatorProperty(doc));
    }

    [Fact]
    public void DetectDiscriminatorProperty_NoCommonProperty_ReturnsNull()
    {
        var doc = ParsePolymorphismDocument();

        Assert.Null(doc.Components!.Schemas!["Mixed"].DetectDiscriminatorProperty(doc));
    }

    [Fact]
    public void DetectDiscriminatorProperty_NotPolymorphic_ReturnsNull()
    {
        var doc = ParsePolymorphismDocument();

        Assert.Null(doc.Components!.Schemas!["Pet"].DetectDiscriminatorProperty(doc));
    }

    // ========== additionalProperties value type Tests ==========
    [Fact]
    public void IsAdditionalPropertiesAnyType_True_ReturnsTrue()
    {
        var doc = ParseDictionaryDocument();

        Assert.True(doc.Components!.Schemas!["AnyBag"].IsAdditionalPropertiesAnyType());
    }

    [Fact]
    public void IsAdditionalPropertiesAnyType_TypedOrAbsent_ReturnsFalse()
    {
        var doc = ParseDictionaryDocument();

        Assert.False(doc.Components!.Schemas!["IntBag"].IsAdditionalPropertiesAnyType());
        Assert.False(doc.Components.Schemas["PetBag"].IsAdditionalPropertiesAnyType());
        Assert.False(doc.Components.Schemas["Pet"].IsAdditionalPropertiesAnyType());
    }

    [Theory]
    [InlineData("AnyBag", "object")]
    [InlineData("IntBag", "long")]
    [InlineData("PetBag", "Pet")]
    [InlineData("Pet", "object")]
    [InlineData("StringListBag", "List<string>")]
    [InlineData("PetListBag", "List<Pet>")]
    [InlineData("NestedBag", "Dictionary<string, int>")]
    public void GetAdditionalPropertiesValueType_ReturnsTheValueType(
        string schemaName,
        string expected)
    {
        var doc = ParseDictionaryDocument();

        Assert.Equal(expected, doc.Components!.Schemas![schemaName].GetAdditionalPropertiesValueType());
    }

    [Fact]
    public void GetAdditionalPropertiesValueType_ConflictingReference_IsFullyQualified()
    {
        var doc = ParseYaml("""
                            openapi: "3.1.0"
                            info:
                              title: T
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Task:
                                  type: object
                                TaskBag:
                                  type: object
                                  additionalProperties:
                                    $ref: '#/components/schemas/Task'
                            """);
        Assert.NotNull(doc);
        var registry = TypeConflictRegistry.Build(doc, "MyApi");

        Assert.Equal("MyApi.Generated.Models.Task", doc.Components!.Schemas!["TaskBag"].GetAdditionalPropertiesValueType(registry));
    }

    // ========== allOf generic pagination Tests ==========
    [Fact]
    public void GetAllOfArrayOverrideItemType_InlineArrayOverride_ReturnsItemType()
    {
        var doc = ParsePaginationDocument();

        Assert.Equal("Account", GetResponseSchema(doc, "/accounts").GetAllOfArrayOverrideItemType(doc));
    }

    [Fact]
    public void GetAllOfArrayOverrideItemType_OverrideOfArrayAlias_ReturnsTheAliasItem()
    {
        var doc = ParsePaginationDocument();

        Assert.Equal("Account", GetResponseSchema(doc, "/aliased").GetAllOfArrayOverrideItemType(doc));
    }

    [Fact]
    public void GetAllOfArrayOverrideItemType_ItemsOfArrayAlias_UnwrapToTheElement()
    {
        var doc = ParsePaginationDocument();

        Assert.Equal("Account", GetResponseSchema(doc, "/nested").GetAllOfArrayOverrideItemType(doc));
    }

    [Fact]
    public void GetAllOfArrayOverrideItemType_NoArrayOverride_ReturnsNull()
    {
        var doc = ParsePaginationDocument();

        Assert.Null(GetResponseSchema(doc, "/plain").GetAllOfArrayOverrideItemType(doc));
        Assert.Null(doc.Components!.Schemas!["Account"].GetAllOfArrayOverrideItemType(doc));
    }

    [Theory]
    [InlineData("/accounts", true, "PaginatedResult<Account>")]
    [InlineData("/accounts", false, "PaginatedResult<Account>?")]
    [InlineData("/plain", true, "PaginatedResult")]
    [InlineData("/plain", false, "PaginatedResult?")]
    public void ToCSharpTypeWithGenericSupport_AllOfWithBase_ReturnsGenericWhenAnArrayIsOverridden(
        string path,
        bool isRequired,
        string expected)
    {
        var doc = ParsePaginationDocument();

        Assert.Equal(expected, GetResponseSchema(doc, path).ToCSharpTypeWithGenericSupport(doc, isRequired));
    }

    [Fact]
    public void ToCSharpTypeWithGenericSupport_ReferenceToArrayAlias_ReturnsElementArray()
    {
        var doc = ParsePaginationDocument();
        var accounts = doc.Components!.Schemas!["AccountList"].Properties!["all"];

        Assert.Equal("Account[]", accounts.ToCSharpTypeWithGenericSupport(doc, isRequired: true));
        Assert.Equal("Account[]?", accounts.ToCSharpTypeWithGenericSupport(doc, isRequired: false));
    }

    [Fact]
    public void ToCSharpTypeWithGenericSupport_PlainReferenceAndPrimitive_FallBackToStandardTypes()
    {
        var doc = ParsePaginationDocument();
        var accountList = doc.Components!.Schemas!["AccountList"];

        Assert.Equal("Account", accountList.Properties!["first"].ToCSharpTypeWithGenericSupport(doc, isRequired: true));
        Assert.Equal("int?", accountList.Properties["count"].ToCSharpTypeWithGenericSupport(doc, isRequired: false));
    }

    // ========== Array schema Tests ==========
    [Fact]
    public void GetArrayTypeName_ItemsReference_ReturnsBracketedName()
    {
        var doc = ParsePaginationDocument();

        Assert.Equal("Account[]", ((OpenApiSchema)doc.Components!.Schemas!["Accounts"]).GetArrayTypeName());
    }

    [Fact]
    public void GetArrayTypeName_PrimitiveItemsAndNoItems()
    {
        Assert.Equal("long[]", new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int64" } }.GetArrayTypeName());
        Assert.Equal("object[]", new OpenApiSchema { Type = JsonSchemaType.Array }.GetArrayTypeName());
    }

    [Fact]
    public void GetArrayItemType_ItemsReference_ResolvesThroughTheRegistry()
    {
        var doc = ParseYaml("""
                            openapi: "3.1.0"
                            info:
                              title: T
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Task:
                                  type: object
                                Tasks:
                                  type: array
                                  items:
                                    $ref: '#/components/schemas/Task'
                            """);
        Assert.NotNull(doc);
        var tasks = (OpenApiSchema)doc.Components!.Schemas!["Tasks"];

        Assert.Equal("Task", tasks.GetArrayItemType());
        Assert.Equal("MyApi.Generated.Models.Task", tasks.GetArrayItemType(TypeConflictRegistry.Build(doc, "MyApi")));
    }

    [Fact]
    public void GetArrayItemType_InlineItemsAndNoItems()
    {
        Assert.Equal("Guid", new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" } }.GetArrayItemType());
        Assert.Equal("object", new OpenApiSchema { Type = JsonSchemaType.Array }.GetArrayItemType());
    }

    private static void AssertNicknameIsNullable(string yaml)
    {
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var person = (OpenApiSchema)document.Components!.Schemas!["Person"];
        var nickname = person.Properties!["nickname"];

        Assert.Equal("string?", nickname.ToCSharpTypeForModel(isRequired: true, registry: null));
    }

    private static OpenApiDocument ParsePolymorphismDocument()
        => OpenApiDocumentHelper.ParseYaml("""
            openapi: "3.1.0"
            info:
              title: T
              version: "1.0.0"
            paths: {}
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    name:
                      type: string
                Cat:
                  type: object
                  properties:
                    type:
                      type: string
                    name:
                      type: string
                Dog:
                  type: object
                  properties:
                    type:
                      type: string
                    bark:
                      type: boolean
                Animal:
                  oneOf:
                    - $ref: '#/components/schemas/Cat'
                    - $ref: '#/components/schemas/Dog'
                  discriminator:
                    propertyName: type
                    mapping:
                      cat: '#/components/schemas/Cat'
                      dog: '#/components/schemas/Dog'
                AnyAnimal:
                  anyOf:
                    - $ref: '#/components/schemas/Cat'
                    - $ref: '#/components/schemas/Dog'
                Mixed:
                  oneOf:
                    - $ref: '#/components/schemas/Pet'
                    - $ref: '#/components/schemas/Dog'
                NullablePet:
                  oneOf:
                    - $ref: '#/components/schemas/Pet'
                    - type: 'null'
                PetOrName:
                  oneOf:
                    - $ref: '#/components/schemas/Pet'
                    - type: string
            """);

    private static OpenApiDocument ParseDictionaryDocument()
        => OpenApiDocumentHelper.ParseYaml("""
            openapi: "3.1.0"
            info:
              title: T
              version: "1.0.0"
            paths: {}
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    name:
                      type: string
                AnyBag:
                  type: object
                  additionalProperties: {}
                IntBag:
                  type: object
                  additionalProperties:
                    type: integer
                    format: int64
                PetBag:
                  type: object
                  additionalProperties:
                    $ref: '#/components/schemas/Pet'
                StringListBag:
                  type: object
                  additionalProperties:
                    type: array
                    items:
                      type: string
                PetListBag:
                  type: object
                  additionalProperties:
                    type: array
                    items:
                      $ref: '#/components/schemas/Pet'
                NestedBag:
                  type: object
                  additionalProperties:
                    type: object
                    additionalProperties:
                      type: integer
                      format: int32
            """);

    private static OpenApiDocument ParsePaginationDocument()
        => OpenApiDocumentHelper.ParseYaml("""
            openapi: "3.0.0"
            info:
              title: T
              version: "1.0.0"
            paths:
              /accounts:
                get:
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
                                      $ref: '#/components/schemas/Account'
              /aliased:
                get:
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
                                    $ref: '#/components/schemas/Accounts'
              /nested:
                get:
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
                                      $ref: '#/components/schemas/Accounts'
              /plain:
                get:
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
                                  note:
                                    type: string
            components:
              schemas:
                Account:
                  type: object
                  properties:
                    id:
                      type: string
                Accounts:
                  type: array
                  items:
                    $ref: '#/components/schemas/Account'
                AccountList:
                  type: object
                  properties:
                    all:
                      $ref: '#/components/schemas/Accounts'
                    first:
                      $ref: '#/components/schemas/Account'
                    count:
                      type: integer
                PaginatedResult:
                  type: object
                  properties:
                    continuationToken:
                      type: string
                    results:
                      type: array
                      items: {}
            """);

    private static IOpenApiSchema GetResponseSchema(
        OpenApiDocument doc,
        string path)
        => doc.Paths[path].Operations!.Values.First().Responses!["200"].Content!["application/json"].Schema!;

    private static OpenApiSchema ParseTupleSchema(string itemsYaml)
    {
        var doc = ParseYaml($$"""
                              openapi: "3.1.0"
                              info:
                                title: T
                                version: "1.0.0"
                              paths: {}
                              components:
                                schemas:
                                  Point:
                                    type: array
                                    prefixItems:
                                      - type: number
                                      - type: number
                                    {{itemsYaml}}
                              """);
        Assert.NotNull(doc);

        return (OpenApiSchema)doc.Components!.Schemas!["Point"];
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}