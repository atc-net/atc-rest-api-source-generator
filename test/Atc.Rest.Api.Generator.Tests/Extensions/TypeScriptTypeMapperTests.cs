namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class TypeScriptTypeMapperTests
{
    // ========== ToTypeScriptTypeName Tests ==========
    [Fact]
    public void ToTypeScriptTypeName_Null_ReturnsUnknown()
    {
        JsonSchemaType? schemaType = null;

        Assert.Equal("unknown", schemaType.ToTypeScriptTypeName());
    }

    [Fact]
    public void ToTypeScriptTypeName_Integer_ReturnsNumber()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Integer;

        Assert.Equal("number", schemaType.ToTypeScriptTypeName());
    }

    [Fact]
    public void ToTypeScriptTypeName_Number_ReturnsNumber()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Number;

        Assert.Equal("number", schemaType.ToTypeScriptTypeName());
    }

    [Fact]
    public void ToTypeScriptTypeName_String_ReturnsString()
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        Assert.Equal("string", schemaType.ToTypeScriptTypeName());
    }

    [Fact]
    public void ToTypeScriptTypeName_Boolean_ReturnsBoolean()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Boolean;

        Assert.Equal("boolean", schemaType.ToTypeScriptTypeName());
    }

    [Fact]
    public void ToTypeScriptTypeName_Array_ReturnsUnknownArray()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Array;

        Assert.Equal("unknown[]", schemaType.ToTypeScriptTypeName());
    }

    [Fact]
    public void ToTypeScriptTypeName_NullableInteger_ReturnsNumber()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Integer | JsonSchemaType.Null;

        Assert.Equal("number", schemaType.ToTypeScriptTypeName());
    }

    [Fact]
    public void ToTypeScriptTypeName_NullableString_ReturnsString()
    {
        JsonSchemaType? schemaType = JsonSchemaType.String | JsonSchemaType.Null;

        Assert.Equal("string", schemaType.ToTypeScriptTypeName());
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
        JsonSchemaType? schemaType = JsonSchemaType.String;

        Assert.Equal(expected, schemaType.ToTypeScriptTypeName(format));
    }

    [Fact]
    public void ToTypeScriptTypeName_DateTimeWithConvertDates_ReturnsDate()
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        Assert.Equal(
            "Date",
            schemaType.ToTypeScriptTypeName("date-time", convertDates: true));
    }

    [Fact]
    public void ToTypeScriptTypeName_DateTimeWithoutConvertDates_ReturnsString()
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        Assert.Equal(
            "string",
            schemaType.ToTypeScriptTypeName("date-time", convertDates: false));
    }

    [Fact]
    public void ToTypeScriptTypeName_DateWithConvertDates_ReturnsDate()
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        Assert.Equal(
            "Date",
            schemaType.ToTypeScriptTypeName("date", convertDates: true));
    }

    // ========== ToTypeScriptTypeForModel Tests ==========
    [Fact]
    public void ToTypeScriptTypeForModel_PrimitiveString_ReturnsString()
    {
        var doc = ParseYaml(YamlWithStringProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "TestModel", "name");
        Assert.NotNull(schema);

        var result = schema!.ToTypeScriptTypeForModel(isRequired: true);

        Assert.Equal("string", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_NullablePrimitive_ReturnsTypeOrNull()
    {
        var doc = ParseYaml(YamlWithNullableProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "TestModel", "description");
        Assert.NotNull(schema);

        var result = schema!.ToTypeScriptTypeForModel(isRequired: false);

        Assert.Equal("string | null", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_DirectRef_ReturnsTypeName()
    {
        var doc = ParseYaml(YamlWithRefProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "Device", "owner");
        Assert.NotNull(schema);

        var result = schema!.ToTypeScriptTypeForModel(isRequired: true);

        Assert.Equal("User", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_ArrayOfPrimitives_ReturnsArrayType()
    {
        var doc = ParseYaml(YamlWithArrayProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "TestModel", "tags");
        Assert.NotNull(schema);

        var result = schema!.ToTypeScriptTypeForModel(isRequired: true);

        Assert.Equal("string[]", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_OneOfSingleRef_ReturnsRefType()
    {
        var doc = ParseYaml(YamlWithOneOfProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "Device", "customer");
        Assert.NotNull(schema);

        var result = schema!.ToTypeScriptTypeForModel(isRequired: false);

        Assert.Equal("IdValue | null", result);
    }

    [Fact]
    public void ToTypeScriptTypeForModel_AllOfSingleRef_ReturnsRefType()
    {
        var doc = ParseYaml(YamlWithAllOfProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "Device", "settings");
        Assert.NotNull(schema);

        var result = schema!.ToTypeScriptTypeForModel(isRequired: false);

        Assert.Equal("DeviceSettings | null", result);
    }

    // ========== ToTypeScriptReturnType Tests ==========
    [Fact]
    public void ToTypeScriptReturnType_PrimitiveString_ReturnsString()
    {
        var doc = ParseYaml(YamlWithStringProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "TestModel", "name");
        Assert.NotNull(schema);

        var result = schema!.ToTypeScriptReturnType();

        Assert.Equal("string", result);
    }

    [Fact]
    public void ToTypeScriptReturnType_RefSchema_ReturnsTypeName()
    {
        var doc = ParseYaml(YamlWithRefProperty);
        Assert.NotNull(doc);

        var schema = GetSchemaProperty(doc!, "Device", "owner");
        Assert.NotNull(schema);

        var result = schema!.ToTypeScriptReturnType();

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