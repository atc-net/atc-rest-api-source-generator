namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class DefaultValueHelperTests
{
    // ========== FormatDefaultValue Tests ==========
    [Theory]
    [InlineData("hello", "string", "hello")]
    [InlineData("\"hello\"", "string", "hello")] // strips quotes
    [InlineData("true", "bool", "true")]
    [InlineData("True", "bool", "true")] // lowercased
    [InlineData("False", "bool", "false")]
    [InlineData("42", "int", "42")]
    [InlineData("3.14", "double", "3.14")]
    [InlineData("100", "long", "100")]
    [InlineData("", "string", null)] // empty returns null
    [InlineData(null, "string", null)] // null returns null
    public void FormatDefaultValue_ReturnsExpectedResult(
        string? rawValue,
        string csharpTypeName,
        string? expected)
    {
        var result = DefaultValueHelper.FormatDefaultValue(rawValue!, csharpTypeName);
        Assert.Equal(expected, result);
    }

    // ========== FormatForAttribute Tests ==========
    [Theory]
    [InlineData("hello", "string", "\"hello\"")]
    [InlineData("true", "bool", "true")]
    [InlineData("True", "bool", "true")]
    [InlineData("42", "int", "42")]
    [InlineData("3.14", "double", "3.14")]
    public void FormatForAttribute_ReturnsExpectedResult(
        string defaultValue,
        string csharpTypeName,
        string expected)
    {
        var result = DefaultValueHelper.FormatForAttribute(defaultValue, csharpTypeName);
        Assert.Equal(expected, result);
    }

    // ========== ExtractSchemaDefault Tests ==========
    [Fact]
    public void ExtractSchemaDefault_NullSchema_ReturnsNull()
    {
        var result = DefaultValueHelper.ExtractSchemaDefault(null, "string");
        Assert.Null(result);
    }
}