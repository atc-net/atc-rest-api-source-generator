namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class StringExtensionsTests
{
    // ========== IsValueType Tests ==========
    [Theory]
    [InlineData("int", true)]
    [InlineData("long", true)]
    [InlineData("bool", true)]
    [InlineData("float", true)]
    [InlineData("double", true)]
    [InlineData("decimal", true)]
    [InlineData("int?", true)]
    [InlineData("long?", true)]
    [InlineData("bool?", true)]
    [InlineData("float?", true)]
    [InlineData("double?", true)]
    [InlineData("decimal?", true)]
    [InlineData("string", false)]
    [InlineData("object", false)]
    [InlineData("Pet", false)]
    [InlineData("int[]", false)]
    [InlineData("List<int>", false)]
    public void IsValueType_ReturnsExpectedResult(
        string type,
        bool expected)
    {
        var result = type.IsValueType();
        Assert.Equal(expected, result);
    }

    // ========== IsNullableValueType Tests ==========
    [Theory]
    [InlineData("int?", true)]
    [InlineData("long?", true)]
    [InlineData("bool?", true)]
    [InlineData("float?", true)]
    [InlineData("double?", true)]
    [InlineData("decimal?", true)]
    [InlineData("int", false)]
    [InlineData("long", false)]
    [InlineData("bool", false)]
    [InlineData("string", false)]
    [InlineData("string?", false)] // string? is not in the list
    [InlineData("Pet?", false)]
    public void IsNullableValueType_ReturnsExpectedResult(
        string type,
        bool expected)
    {
        var result = type.IsNullableValueType();
        Assert.Equal(expected, result);
    }

    // ========== IsArrayType Tests ==========
    [Theory]
    [InlineData("int[]", true)]
    [InlineData("string[]", true)]
    [InlineData("Pet[]", true)]
    [InlineData("object[]", true)]
    [InlineData("int", false)]
    [InlineData("string", false)]
    [InlineData("List<int>", false)]
    [InlineData("IEnumerable<string>", false)]
    public void IsArrayType_ReturnsExpectedResult(
        string type,
        bool expected)
    {
        var result = type.IsArrayType();
        Assert.Equal(expected, result);
    }

    // ========== IsReferenceType Tests ==========
    [Theory]
    [InlineData("string", true)]
    [InlineData("object", true)]
    [InlineData("Pet", true)]
    [InlineData("int[]", true)] // Arrays are reference types
    [InlineData("string[]", true)]
    [InlineData("List<int>", true)]
    [InlineData("int", false)]
    [InlineData("long", false)]
    [InlineData("bool", false)]
    [InlineData("int?", false)] // Nullable value types are still value types
    public void IsReferenceType_ReturnsExpectedResult(
        string type,
        bool expected)
    {
        var result = type.IsReferenceType();
        Assert.Equal(expected, result);
    }
}