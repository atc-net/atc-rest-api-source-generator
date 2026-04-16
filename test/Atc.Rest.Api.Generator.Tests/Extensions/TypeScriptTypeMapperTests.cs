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

        var schema = GetSchemaProperty(doc!, "TestModel", "name");
        Assert.NotNull(schema);

        // Act
        var result = schema!.ToTypeScriptTypeForModel(isRequired: true);

        // Assert
        Assert.Equal("string", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_NullablePrimitive_ReturnsTypeOrNull()
    {
        // Arrange
        var doc = ParseYaml(YamlWithNullableProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "TestModel", "description");
        Assert.NotNull(schema);

        // Act
        var result = schema!.ToTypeScriptTypeForModel(isRequired: false);

        // Assert
        Assert.Equal("string | null", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_DirectRef_ReturnsTypeName()
    {
        // Arrange
        var doc = ParseYaml(YamlWithRefProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "Device", "owner");
        Assert.NotNull(schema);

        // Act
        var result = schema!.ToTypeScriptTypeForModel(isRequired: true);

        // Assert
        Assert.Equal("User", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_ArrayOfPrimitives_ReturnsArrayType()
    {
        // Arrange
        var doc = ParseYaml(YamlWithArrayProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "TestModel", "tags");
        Assert.NotNull(schema);

        // Act
        var result = schema!.ToTypeScriptTypeForModel(isRequired: true);

        // Assert
        Assert.Equal("string[]", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_OneOfSingleRef_ReturnsRefType()
    {
        // Arrange
        var doc = ParseYaml(YamlWithOneOfProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "Device", "customer");
        Assert.NotNull(schema);

        // Act
        var result = schema!.ToTypeScriptTypeForModel(isRequired: false);

        // Assert
        Assert.Equal("IdValue | null", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_AllOfSingleRef_ReturnsRefType()
    {
        // Arrange
        var doc = ParseYaml(YamlWithAllOfProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "Device", "settings");
        Assert.NotNull(schema);

        // Act
        var result = schema!.ToTypeScriptTypeForModel(isRequired: false);

        // Assert
        Assert.Equal("DeviceSettings | null", result);
    }

    // ========== ToTypeScriptReturnType Tests ==========
    [Fact]
    public void ToTypeScriptReturnType_PrimitiveString_ReturnsString()
    {
        // Arrange
        var doc = ParseYaml(YamlWithStringProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "TestModel", "name");
        Assert.NotNull(schema);

        // Act
        var result = schema!.ToTypeScriptReturnType();

        // Assert
        Assert.Equal("string", result);
    }

    [Fact]
    public void ToTypeScriptReturnType_RefSchema_ReturnsTypeName()
    {
        // Arrange
        var doc = ParseYaml(YamlWithRefProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "Device", "owner");
        Assert.NotNull(schema);

        // Act
        var result = schema!.ToTypeScriptReturnType();

        // Assert
        Assert.Equal("User", result);
    }

    // ========== Helper Methods ==========
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
        if (doc.Components?.Schemas == null)
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