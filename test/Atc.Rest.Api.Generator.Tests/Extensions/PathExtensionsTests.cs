namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class PathExtensionsTests
{
    // ========== MatchesPathSegment Tests ==========
    [Theory]
    [InlineData("/pets/{petId}", "Pets", true)]
    [InlineData("/pets/{petId}", "Users", false)]
    [InlineData("/api/v1/pets", "Pets", true)]
    [InlineData("/users/{id}/orders", "Users", true)]
    [InlineData("/pets", null, true)] // null segment matches all
    [InlineData("/pets", "", true)] // empty segment matches all
    [InlineData("/pets", "pets", true)] // case-insensitive
    public void MatchesPathSegment_ReturnsExpectedResult(
        string path,
        string? pathSegment,
        bool expected)
    {
        var result = path.MatchesPathSegment(pathSegment);
        Assert.Equal(expected, result);
    }

    // ========== ShouldSkipForPathSegment Tests ==========
    [Theory]
    [InlineData("/pets/{petId}", "Pets", false)] // matches -> don't skip
    [InlineData("/pets/{petId}", "Users", true)] // doesn't match -> skip
    [InlineData("/users/{id}", null, false)] // null segment -> never skip
    [InlineData("/api/v1/pets", "Pets", false)] // skips api/v1 prefix, matches pets
    public void ShouldSkipForPathSegment_ReturnsExpectedResult(
        string path,
        string? pathSegment,
        bool expected)
    {
        var result = path.ShouldSkipForPathSegment(pathSegment);
        Assert.Equal(expected, result);
    }
}