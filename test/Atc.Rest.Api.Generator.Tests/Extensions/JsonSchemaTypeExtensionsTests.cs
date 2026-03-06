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
    [InlineData("date", "DateTimeOffset")]
    [InlineData("date-time", "DateTimeOffset")]
    [InlineData("guid", "Guid")]
    [InlineData("int", "int")]
    [InlineData("int32", "int")]
    [InlineData("int64", "long")]
    [InlineData("long", "long")]
    [InlineData("byte", "byte[]")]
    [InlineData("unknown", "string")]
    [InlineData("uri", "Uri")]
    [InlineData("uuid", "Guid")]
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

    [Fact]
    public void ToCSharpTypeName_NumberWithNullFlag_ReturnsDouble()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Number | JsonSchemaType.Null;

        var result = schemaType.ToCSharpTypeName();

        Assert.Equal("double", result);
    }

    [Fact]
    public void ToCSharpTypeName_BooleanWithNullFlag_ReturnsBool()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Boolean | JsonSchemaType.Null;

        var result = schemaType.ToCSharpTypeName();

        Assert.Equal("bool", result);
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

    // ========== ToPrimitiveCSharpTypeName - Additional Coverage ==========
    [Fact]
    public void ToPrimitiveCSharpTypeName_Number_ReturnsDouble()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Number;

        var result = schemaType.ToPrimitiveCSharpTypeName();

        Assert.Equal("double", result);
    }

    [Fact]
    public void ToPrimitiveCSharpTypeName_Boolean_ReturnsBool()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Boolean;

        var result = schemaType.ToPrimitiveCSharpTypeName();

        Assert.Equal("bool", result);
    }

    [Fact]
    public void ToPrimitiveCSharpTypeName_Object_ReturnsObject()
    {
        JsonSchemaType? schemaType = JsonSchemaType.Object;

        var result = schemaType.ToPrimitiveCSharpTypeName();

        Assert.Equal("object", result);
    }

    // ========== CountNonNullTypes Tests ==========
    [Fact]
    public void CountNonNullTypes_SingleType_ReturnsOne()
    {
        var type = JsonSchemaType.String;

        Assert.Equal(1, type.CountNonNullTypes());
    }

    [Fact]
    public void CountNonNullTypes_CombinedTypes_ReturnsCount()
    {
        var type = JsonSchemaType.String | JsonSchemaType.Integer;

        Assert.Equal(2, type.CountNonNullTypes());
    }

    [Fact]
    public void CountNonNullTypes_WithNullFlag_ExcludesNull()
    {
        var type = JsonSchemaType.String | JsonSchemaType.Null;

        Assert.Equal(1, type.CountNonNullTypes());
    }

    [Fact]
    public void CountNonNullTypes_NullOnly_ReturnsZero()
    {
        var type = JsonSchemaType.Null;

        Assert.Equal(0, type.CountNonNullTypes());
    }

    [Fact]
    public void CountNonNullTypes_AllTypes_ReturnsSix()
    {
        var type = JsonSchemaType.String | JsonSchemaType.Integer | JsonSchemaType.Number |
                   JsonSchemaType.Boolean | JsonSchemaType.Array | JsonSchemaType.Object;

        Assert.Equal(6, type.CountNonNullTypes());
    }

    [Fact]
    public void CountNonNullTypes_Nullable_NullInput_ReturnsZero()
    {
        JsonSchemaType? type = null;

        Assert.Equal(0, type.CountNonNullTypes());
    }

    // ========== GetPrimaryType Tests ==========
    [Fact]
    public void GetPrimaryType_StringOnly_ReturnsString()
    {
        var type = JsonSchemaType.String;

        Assert.Equal(JsonSchemaType.String, type.GetPrimaryType());
    }

    [Fact]
    public void GetPrimaryType_StringAndInteger_PrioritizesString()
    {
        var type = JsonSchemaType.String | JsonSchemaType.Integer;

        Assert.Equal(JsonSchemaType.String, type.GetPrimaryType());
    }

    [Fact]
    public void GetPrimaryType_IntegerAndNumber_PrioritizesInteger()
    {
        var type = JsonSchemaType.Integer | JsonSchemaType.Number;

        Assert.Equal(JsonSchemaType.Integer, type.GetPrimaryType());
    }

    [Fact]
    public void GetPrimaryType_WithNullFlag_StripsNullFirst()
    {
        var type = JsonSchemaType.Boolean | JsonSchemaType.Null;

        Assert.Equal(JsonSchemaType.Boolean, type.GetPrimaryType());
    }

    [Fact]
    public void GetPrimaryType_NullOnly_ReturnsNull()
    {
        var type = JsonSchemaType.Null;

        Assert.Null(type.GetPrimaryType());
    }

    [Fact]
    public void GetPrimaryType_Nullable_NullInput_ReturnsNull()
    {
        JsonSchemaType? type = null;

        Assert.Null(type.GetPrimaryType());
    }

    // ========== GetNonNullTypeNames Tests ==========
    [Fact]
    public void GetNonNullTypeNames_SingleType_ReturnsSingleName()
    {
        var type = JsonSchemaType.String;

        var result = type.GetNonNullTypeNames();

        Assert.Single(result);
        Assert.Equal("string", result[0]);
    }

    [Fact]
    public void GetNonNullTypeNames_MultipleTypes_ReturnsAllNames()
    {
        var type = JsonSchemaType.String | JsonSchemaType.Integer | JsonSchemaType.Boolean;

        var result = type.GetNonNullTypeNames();

        Assert.Equal(3, result.Count);
        Assert.Contains("string", result);
        Assert.Contains("integer", result);
        Assert.Contains("boolean", result);
    }

    [Fact]
    public void GetNonNullTypeNames_WithNull_ExcludesNull()
    {
        var type = JsonSchemaType.String | JsonSchemaType.Null;

        var result = type.GetNonNullTypeNames();

        Assert.Single(result);
        Assert.Equal("string", result[0]);
    }

    [Fact]
    public void GetNonNullTypeNames_NullOnly_ReturnsEmpty()
    {
        var type = JsonSchemaType.Null;

        var result = type.GetNonNullTypeNames();

        Assert.Empty(result);
    }

    [Fact]
    public void GetNonNullTypeNames_Nullable_NullInput_ReturnsEmpty()
    {
        JsonSchemaType? type = null;

        var result = type.GetNonNullTypeNames();

        Assert.Empty(result);
    }
}