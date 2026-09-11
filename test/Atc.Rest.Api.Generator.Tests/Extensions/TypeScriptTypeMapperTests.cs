namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class TypeScriptTypeMapperTests
{
    // ========== ToTypeScriptTypeName Tests ==========
    [Fact]
    public void ToTypeScriptTypeName_Null_ReturnsUnknown()
    {
        // Arrange
        JsonSchemaType? schemaType = null;

        // Act
        var result = schemaType.ToTypeScriptTypeName();

        // Assert
        Assert.Equal("unknown", result);
    }

    [Fact]
    public void ToTypeScriptTypeName_Integer_ReturnsNumber()
    {
        // Arrange
        JsonSchemaType? schemaType = JsonSchemaType.Integer;

        // Act
        var result = schemaType.ToTypeScriptTypeName();

        // Assert
        Assert.Equal("number", result);
    }

    [Fact]
    public void ToTypeScriptTypeName_Number_ReturnsNumber()
    {
        // Arrange
        JsonSchemaType? schemaType = JsonSchemaType.Number;

        // Act
        var result = schemaType.ToTypeScriptTypeName();

        // Assert
        Assert.Equal("number", result);
    }

    [Fact]
    public void ToTypeScriptTypeName_String_ReturnsString()
    {
        // Arrange
        JsonSchemaType? schemaType = JsonSchemaType.String;

        // Act
        var result = schemaType.ToTypeScriptTypeName();

        // Assert
        Assert.Equal("string", result);
    }

    [Fact]
    public void ToTypeScriptTypeName_Boolean_ReturnsBoolean()
    {
        // Arrange
        JsonSchemaType? schemaType = JsonSchemaType.Boolean;

        // Act
        var result = schemaType.ToTypeScriptTypeName();

        // Assert
        Assert.Equal("boolean", result);
    }

    [Fact]
    public void ToTypeScriptTypeName_Array_ReturnsUnknownArray()
    {
        // Arrange
        JsonSchemaType? schemaType = JsonSchemaType.Array;

        // Act
        var result = schemaType.ToTypeScriptTypeName();

        // Assert
        Assert.Equal("unknown[]", result);
    }

    [Fact]
    public void ToTypeScriptTypeName_NullableInteger_ReturnsNumber()
    {
        // Arrange
        JsonSchemaType? schemaType = JsonSchemaType.Integer | JsonSchemaType.Null;

        // Act
        var result = schemaType.ToTypeScriptTypeName();

        // Assert
        Assert.Equal("number", result);
    }

    [Fact]
    public void ToTypeScriptTypeName_NullableString_ReturnsString()
    {
        // Arrange
        JsonSchemaType? schemaType = JsonSchemaType.String | JsonSchemaType.Null;

        // Act
        var result = schemaType.ToTypeScriptTypeName();

        // Assert
        Assert.Equal("string", result);
    }

    // ========== String Format Tests ==========
    [Theory]
    [InlineData("binary", "Blob | File")]
    [InlineData("byte", "string")]
    [InlineData("uuid", "string")]
    [InlineData("guid", "string")]
    [InlineData("uri", "string")]
    [InlineData(null, "string")]
    public void ToTypeScriptTypeName_StringWithFormat_ReturnsExpected(
        string? format,
        string expected)
    {
        // Arrange
        JsonSchemaType? schemaType = JsonSchemaType.String;

        // Act
        var result = schemaType.ToTypeScriptTypeName(format);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToTypeScriptTypeName_DateTimeWithConvertDates_ReturnsDate()
    {
        // Arrange
        JsonSchemaType? schemaType = JsonSchemaType.String;

        // Act
        var result = schemaType.ToTypeScriptTypeName("date-time", convertDates: true);

        // Assert
        Assert.Equal("Date", result);
    }

    [Fact]
    public void ToTypeScriptTypeName_DateTimeWithoutConvertDates_ReturnsString()
    {
        // Arrange
        JsonSchemaType? schemaType = JsonSchemaType.String;

        // Act
        var result = schemaType.ToTypeScriptTypeName("date-time", convertDates: false);

        // Assert
        Assert.Equal("string", result);
    }

    [Fact]
    public void ToTypeScriptTypeName_DateWithConvertDates_ReturnsDate()
    {
        // Arrange
        JsonSchemaType? schemaType = JsonSchemaType.String;

        // Act
        var result = schemaType.ToTypeScriptTypeName("date", convertDates: true);

        // Assert
        Assert.Equal("Date", result);
    }

    // ========== ToTypeScriptTypeForModel Tests ==========
    [Fact]
    public void ToTypeScriptTypeForModel_PrimitiveString_ReturnsString()
    {
        // Arrange
        var doc = ParseYaml(YamlWithStringProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc, "TestModel", "name");
        Assert.NotNull(schema);

        // Act
        var result = schema.ToTypeScriptTypeForModel(isRequired: true);

        // Assert
        Assert.Equal("string", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_NullablePrimitive_ReturnsTypeOrNull()
    {
        // Arrange
        var doc = ParseYaml(YamlWithNullableProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc, "TestModel", "description");
        Assert.NotNull(schema);

        // Act
        var result = schema.ToTypeScriptTypeForModel(isRequired: false);

        // Assert
        Assert.Equal("string | null", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_DirectRef_ReturnsTypeName()
    {
        // Arrange
        var doc = ParseYaml(YamlWithRefProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc, "Device", "owner");
        Assert.NotNull(schema);

        // Act
        var result = schema.ToTypeScriptTypeForModel(isRequired: true);

        // Assert
        Assert.Equal("User", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_ArrayOfPrimitives_ReturnsArrayType()
    {
        // Arrange
        var doc = ParseYaml(YamlWithArrayProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc, "TestModel", "tags");
        Assert.NotNull(schema);

        // Act
        var result = schema.ToTypeScriptTypeForModel(isRequired: true);

        // Assert
        Assert.Equal("string[]", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_OneOfSingleRef_ReturnsRefType()
    {
        // Arrange
        var doc = ParseYaml(YamlWithOneOfProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc, "Device", "customer");
        Assert.NotNull(schema);

        // Act
        var result = schema.ToTypeScriptTypeForModel(isRequired: false);

        // Assert
        // Spec note: `nullable: true` applies only when `type` is in the same Schema Object, and
        // never reaches through allOf/oneOf (OAI "Clarify Semantics of nullable", 2019).
        // Microsoft.OpenApi 3.7.0 honoured it here anyway; 3.10.2 correctly does not, so the
        // property is no longer nullable. The 3.1 spelling — `oneOf: [$ref, {type: "null"}]` —
        // does produce `IdValue | null`; see ToTypeScriptTypeForModel_OneOfRefPlusNullBranch.
        Assert.Equal("IdValue", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_AllOfSingleRef_ReturnsRefType()
    {
        // Arrange
        var doc = ParseYaml(YamlWithAllOfProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc, "Device", "settings");
        Assert.NotNull(schema);

        // Act
        var result = schema.ToTypeScriptTypeForModel(isRequired: false);

        // Assert
        // See the spec note above: nullable does not reach through allOf.
        Assert.Equal("DeviceSettings", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_OneOfRefPlusNullBranch_IsNullableRefType()
    {
        // The OpenAPI 3.1 spelling of a nullable reference. The null branch marks nullability
        // rather than adding a union member, so this stays a single named type.
        var doc = ParseYaml("""
                            openapi: "3.1.0"
                            info:
                              title: T
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Device:
                                  type: object
                                  properties:
                                    customer:
                                      oneOf:
                                        - $ref: '#/components/schemas/IdValue'
                                        - type: "null"
                                IdValue:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                            """);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc, "Device", "customer");
        Assert.NotNull(schema);

        Assert.Equal("IdValue | null", schema.ToTypeScriptTypeForModel(isRequired: true));
    }

    // ========== ToTypeScriptReturnType Tests ==========
    [Fact]
    public void ToTypeScriptReturnType_PrimitiveString_ReturnsString()
    {
        // Arrange
        var doc = ParseYaml(YamlWithStringProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc, "TestModel", "name");
        Assert.NotNull(schema);

        // Act
        var result = schema.ToTypeScriptReturnType();

        // Assert
        Assert.Equal("string", result);
    }

    [Fact]
    public void ToTypeScriptReturnType_RefSchema_ReturnsTypeName()
    {
        // Arrange
        var doc = ParseYaml(YamlWithRefProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc, "Device", "owner");
        Assert.NotNull(schema);

        // Act
        var result = schema.ToTypeScriptReturnType();

        // Assert
        Assert.Equal("User", result);
    }

    [Fact]
    public void ToTypeScriptReturnType_PrefixItems_TwoNumbers_ReturnsNumberTuple()
    {
        // Coordinate from OpenApi31Features.yaml — prefixItems with two number positions.
        // Today this returns "unknown[]" which loses the tuple shape and length constraint.
        // After the fix: "[number, number]".
        var doc = ParseYaml(YamlWithPrefixItemsCoordinate);
        Assert.NotNull(doc);
        var schema = GetSchema(doc, "Coordinate");
        Assert.NotNull(schema);

        var result = schema.ToTypeScriptReturnType();

        Assert.Equal("[number, number]", result);
    }

    [Fact]
    public void ToTypeScriptReturnType_PrefixItems_ThreeIntegers_ReturnsNumberTuple()
    {
        // RgbColor pattern — three integers (red, green, blue).
        var doc = ParseYaml(YamlWithPrefixItemsRgb);
        Assert.NotNull(doc);
        var schema = GetSchema(doc, "RgbColor");
        Assert.NotNull(schema);

        var result = schema.ToTypeScriptReturnType();

        Assert.Equal("[number, number, number]", result);
    }

    [Fact]
    public void ToTypeScriptReturnType_PrefixItems_MixedTypes_ReturnsMixedTuple()
    {
        // [string, number] — heterogeneous tuple. Each prefix item has its own type.
        var doc = ParseYaml(YamlWithPrefixItemsMixed);
        Assert.NotNull(doc);
        var schema = GetSchema(doc, "NamedScore");
        Assert.NotNull(schema);

        var result = schema.ToTypeScriptReturnType();

        Assert.Equal("[string, number]", result);
    }

    [Fact]
    public void ToTypeScriptReturnType_PrefixItems_OpenTuple_EmitsRestElement()
    {
        // prefixItems with a regular `items: { type: string }` — additional elements
        // beyond the prefix must be the `items` type. TS tuple syntax: [..., ...string[]].
        var doc = ParseYaml(YamlWithPrefixItemsOpenTuple);
        Assert.NotNull(doc);
        var schema = GetSchema(doc, "RowWithLabels");
        Assert.NotNull(schema);

        var result = schema.ToTypeScriptReturnType();

        Assert.Equal("[number, number, ...string[]]", result);
    }

    private static IOpenApiSchema? GetSchema(
        OpenApiDocument doc,
        string schemaName)
    {
        if (doc.Components?.Schemas is null)
        {
            return null;
        }

        return doc.Components.Schemas.TryGetValue(schemaName, out var schemaValue)
            ? schemaValue
            : null;
    }

    private const string YamlWithPrefixItemsCoordinate = """
        openapi: 3.1.0
        info:
          title: Test API
          version: 1.0.0
        paths: {}
        components:
          schemas:
            Coordinate:
              type: array
              prefixItems:
                - type: number
                - type: number
              items: false
        """;

    private const string YamlWithPrefixItemsRgb = """
        openapi: 3.1.0
        info:
          title: Test API
          version: 1.0.0
        paths: {}
        components:
          schemas:
            RgbColor:
              type: array
              prefixItems:
                - type: integer
                - type: integer
                - type: integer
              items: false
        """;

    private const string YamlWithPrefixItemsMixed = """
        openapi: 3.1.0
        info:
          title: Test API
          version: 1.0.0
        paths: {}
        components:
          schemas:
            NamedScore:
              type: array
              prefixItems:
                - type: string
                - type: number
              items: false
        """;

    private const string YamlWithPrefixItemsOpenTuple = """
        openapi: 3.1.0
        info:
          title: Test API
          version: 1.0.0
        paths: {}
        components:
          schemas:
            RowWithLabels:
              type: array
              prefixItems:
                - type: number
                - type: number
              items:
                type: string
        """;

    // ========== Existing helpers continue below ==========
    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(
            yaml,
            "test.yaml",
            out var document)
            ? document
            : null;

    private static IOpenApiSchema? GetSchemaProperty(
        OpenApiDocument doc,
        string schemaName,
        string propertyName)
    {
        if (doc.Components?.Schemas is null)
        {
            return null;
        }

        if (!doc.Components.Schemas.TryGetValue(schemaName, out var schemaValue))
        {
            return null;
        }

        if (schemaValue is not OpenApiSchema schema)
        {
            return null;
        }

        return schema.Properties.TryGetValue(propertyName, out var property)
            ? property
            : null;
    }

    // ========== YAML Test Data ==========
    private const string YamlWithStringProperty = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths: {}
        components:
          schemas:
            TestModel:
              type: object
              properties:
                name:
                  type: string
        """;

    private const string YamlWithNullableProperty = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths: {}
        components:
          schemas:
            TestModel:
              type: object
              properties:
                description:
                  type: string
                  nullable: true
        """;

    private const string YamlWithRefProperty = """
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
                  $ref: '#/components/schemas/User'
            User:
              type: object
              properties:
                name:
                  type: string
        """;

    private const string YamlWithArrayProperty = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths: {}
        components:
          schemas:
            TestModel:
              type: object
              properties:
                tags:
                  type: array
                  items:
                    type: string
        """;

    private const string YamlWithOneOfProperty = """
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
        """;

    private const string YamlWithAllOfProperty = """
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
}