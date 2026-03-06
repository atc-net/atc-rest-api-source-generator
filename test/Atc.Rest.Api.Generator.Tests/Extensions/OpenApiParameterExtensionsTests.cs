namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class OpenApiParameterExtensionsTests
{
    // ========== Resolve Tests ==========
    [Fact]
    public void Resolve_DirectParameter_ReturnsParameterWithNullReferenceId()
    {
        IOpenApiParameter param = new OpenApiParameter { Name = "id", In = ParameterLocation.Path };

        var result = param.Resolve();

        Assert.NotNull(result.Parameter);
        Assert.Equal("id", result.Parameter!.Name);
        Assert.Null(result.ReferenceId);
    }

    // ========== GetName Tests ==========
    [Fact]
    public void GetName_DirectParameter_ReturnsName()
    {
        IOpenApiParameter param = new OpenApiParameter { Name = "petId" };

        var result = param.GetName();

        Assert.Equal("petId", result);
    }

    // ========== ToCSharpType Tests ==========
    [Fact]
    public void ToCSharpType_NoSchema_ReturnsString()
    {
        var param = new OpenApiParameter { Name = "id" };

        var result = param.ToCSharpType();

        Assert.Equal("string", result);
    }

    // ========== GetBindingAttributeName Tests ==========
    [Theory]
    [InlineData(ParameterLocation.Query, "FromQuery")]
    [InlineData(ParameterLocation.Path, "FromRoute")]
    [InlineData(ParameterLocation.Header, "FromHeader")]
    [InlineData(ParameterLocation.Cookie, "FromCookie")]
    public void GetBindingAttributeName_ReturnsExpected(
        ParameterLocation location,
        string expected)
    {
        var param = new OpenApiParameter { Name = "test", In = location };

        var result = param.GetBindingAttributeName();

        Assert.Equal(expected, result);
    }

    // ========== IsValueType Tests ==========
    [Fact]
    public void IsValueType_IntegerSchema_ReturnsTrue()
    {
        var param = CreateParameterWithType(JsonSchemaType.Integer);

        Assert.True(param.IsValueType());
    }

    [Fact]
    public void IsValueType_NumberSchema_ReturnsTrue()
    {
        var param = CreateParameterWithType(JsonSchemaType.Number);

        Assert.True(param.IsValueType());
    }

    [Fact]
    public void IsValueType_BooleanSchema_ReturnsTrue()
    {
        var param = CreateParameterWithType(JsonSchemaType.Boolean);

        Assert.True(param.IsValueType());
    }

    [Fact]
    public void IsValueType_StringSchema_ReturnsFalse()
    {
        var param = CreateParameterWithType(JsonSchemaType.String);

        Assert.False(param.IsValueType());
    }

    [Fact]
    public void IsValueType_NullableInteger_ReturnsTrue()
    {
        var param = CreateParameterWithType(JsonSchemaType.Integer | JsonSchemaType.Null);

        Assert.True(param.IsValueType());
    }

    [Fact]
    public void IsValueType_NoSchema_ReturnsFalse()
    {
        var param = new OpenApiParameter { Name = "test" };

        Assert.False(param.IsValueType());
    }

    // ========== Helper Methods ==========
    private static OpenApiParameter CreateParameterWithType(JsonSchemaType type)
        => new()
        {
            Name = "test",
            Schema = new OpenApiSchema { Type = type },
        };
}