namespace Atc.OpenApi.Tests.Helpers;

public sealed class PaginationConventionsTests
{
    [Theory]
    [InlineData("items", true)]
    [InlineData("results", true)]
    [InlineData("data", true)]
    [InlineData("records", true)]
    [InlineData("values", true)]
    [InlineData("content", true)]
    [InlineData("Items", true)]
    [InlineData("CONTENT", true)]
    [InlineData("result", false)]
    [InlineData("entries", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsArrayPropertyName_KnowsTheSixPageArrayNamesInAnyCase(
        string? propertyName,
        bool expected)
        => Assert.Equal(expected, PaginationConventions.IsArrayPropertyName(propertyName));
}