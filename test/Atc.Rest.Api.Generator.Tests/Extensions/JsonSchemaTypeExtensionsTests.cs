namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class JsonSchemaTypeExtensionsTests
{
    // ========== ToCSharpTypeName - Integer Tests ==========
    [Theory]
    [InlineData(null, "int")]
    [InlineData("int32", "int")]
    [InlineData("int64", "long")]
    [InlineData("unknown", "int")]
    public void ToCSharpTypeName_Integer_ReturnsExpectedType(
        string? format,
        string expected)
    {
        JsonSchemaType? schemaType = JsonSchemaType.Integer;

        var result = schemaType.ToCSharpTypeName(format);

        Assert.Equal(expected, result);
    }

    // ========== ToCSharpTypeName - Number Tests ==========
    [Theory]
    [InlineData(null, "double")]
    [InlineData("int32", "int")]
    [InlineData("int64", "long")]
    [InlineData("float", "float")]
    [InlineData("double", "double")]
    [InlineData("unknown", "double")]
    public void ToCSharpTypeName_Number_ReturnsExpectedType(
        string? format,
        string expected)
    {
        JsonSchemaType? schemaType = JsonSchemaType.Number;

        var result = schemaType.ToCSharpTypeName(format);

        Assert.Equal(expected, result);
    }

    // ========== ToCSharpTypeName - String Tests ==========
    [Theory]
    [InlineData(null, "string")]
    [InlineData("uuid", "Guid")]
    [InlineData("date-time", "DateTimeOffset")]
    [InlineData("date", "DateTimeOffset")]
    [InlineData("uri", "Uri")]
    [InlineData("unknown", "string")]
    public void ToCSharpTypeName_String_ReturnsExpectedType(
        string? format,
        string expected)
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        var result = schemaType.ToCSharpTypeName(format);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToCSharpTypeName_String_Binary_WithoutIFormFile_ReturnsString()
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        var result = schemaType.ToCSharpTypeName("binary", includeIFormFile: false);

        Assert.Equal("string", result);
    }

    [Fact]
    public void ToCSharpTypeName_String_Binary_WithIFormFile_ReturnsIFormFile()
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        var result = schemaType.ToCSharpTypeName("binary", includeIFormFile: true);

        Assert.Equal("IFormFile", result);
    }

    [Theory]
    [InlineData("Binary")]
    [InlineData("BINARY")]
    public void ToCSharpTypeName_String_Binary_CaseInsensitive(string format)
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        var result = schemaType.ToCSharpTypeName(format, includeIFormFile: true);

        Assert.Equal("IFormFile", result);
    }

    // ========== ToCSharpTypeName - Boolean Tests ==========
    [Fact]
    public void ToCSharpTypeName_Boolean_ReturnsBool()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Boolean;

        var result = schemaType.ToCSharpTypeName();

        Assert.Equal("bool", result);
    }

    // ========== ToCSharpTypeName - Array Tests ==========
    [Fact]
    public void ToCSharpTypeName_Array_ReturnsObjectArray()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Array;

        var result = schemaType.ToCSharpTypeName();

        Assert.Equal("object[]", result);
    }

    // ========== ToCSharpTypeName - Object Tests ==========
    [Fact]
    public void ToCSharpTypeName_Object_ReturnsObject()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Object;

        var result = schemaType.ToCSharpTypeName();

        Assert.Equal("object", result);
    }

    // ========== ToCSharpTypeName - Null Handling Tests ==========
    [Fact]
    public void ToCSharpTypeName_NullSchemaType_ReturnsObject()
    {
        JsonSchemaType? schemaType = null;

        var result = schemaType.ToCSharpTypeName();

        Assert.Equal("object", result);
    }

    [Fact]
    public void ToCSharpTypeName_StringWithNullFlag_ReturnsString()
    {
        // OpenAPI 3.1 uses combined flags for nullable types
        JsonSchemaType? schemaType = JsonSchemaType.String | JsonSchemaType.Null;

        var result = schemaType.ToCSharpTypeName();

        Assert.Equal("string", result);
    }

    [Fact]
    public void ToCSharpTypeName_IntegerWithNullFlag_ReturnsInt()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Integer | JsonSchemaType.Null;

        var result = schemaType.ToCSharpTypeName();

        Assert.Equal("int", result);
    }

    // ========== ToPrimitiveCSharpTypeName Tests ==========
    [Fact]
    public void ToPrimitiveCSharpTypeName_Array_ReturnsNull()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Array;

        var result = schemaType.ToPrimitiveCSharpTypeName();

        Assert.Null(result);
    }

    [Fact]
    public void ToPrimitiveCSharpTypeName_Integer_ReturnsInt()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Integer;

        var result = schemaType.ToPrimitiveCSharpTypeName();

        Assert.Equal("int", result);
    }

    [Fact]
    public void ToPrimitiveCSharpTypeName_String_ReturnsString()
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        var result = schemaType.ToPrimitiveCSharpTypeName();

        Assert.Equal("string", result);
    }

    [Fact]
    public void ToPrimitiveCSharpTypeName_NullSchemaType_ReturnsObject()
    {
        JsonSchemaType? schemaType = null;

        var result = schemaType.ToPrimitiveCSharpTypeName();

        Assert.Equal("object", result);
    }

    [Fact]
    public void ToPrimitiveCSharpTypeName_String_Binary_WithIFormFile_ReturnsIFormFile()
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        var result = schemaType.ToPrimitiveCSharpTypeName("binary", includeIFormFile: true);

        Assert.Equal("IFormFile", result);
    }

    [Fact]
    public void ToPrimitiveCSharpTypeName_ArrayWithNullFlag_ReturnsNull()
    {
        // Even combined with Null flag, Array should still return null
        JsonSchemaType? schemaType = JsonSchemaType.Array | JsonSchemaType.Null;

        var result = schemaType.ToPrimitiveCSharpTypeName();

        Assert.Null(result);
    }

    // ========== Format Edge Cases ==========
    [Theory]
    [InlineData("UUID")]
    [InlineData("Uuid")]
    public void ToCSharpTypeName_String_Uuid_CaseInsensitive(string format)
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        var result = schemaType.ToCSharpTypeName(format);

        Assert.Equal("Guid", result);
    }

    [Theory]
    [InlineData("DATE-TIME")]
    [InlineData("Date-Time")]
    public void ToCSharpTypeName_String_DateTime_CaseInsensitive(string format)
    {
        JsonSchemaType? schemaType = JsonSchemaType.String;

        var result = schemaType.ToCSharpTypeName(format);

        Assert.Equal("DateTimeOffset", result);
    }
}