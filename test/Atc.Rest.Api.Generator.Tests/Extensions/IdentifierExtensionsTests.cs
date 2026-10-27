namespace Atc.Rest.Api.Generator.Tests.Extensions;

public sealed class IdentifierExtensionsTests
{
    [Fact]
    public void Claim_NumbersANameAlreadyTaken()
    {
        var taken = new HashSet<string>(["id"], StringComparer.Ordinal);

        Assert.Equal("id2", taken.Claim("id"));
        Assert.Equal("id3", taken.Claim("id"));
        Assert.Equal("name", taken.Claim("name"));
        Assert.Equal(["id", "id2", "id3", "name"], taken.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void Claim_FollowsTheComparerOfTheSet()
    {
        var taken = new HashSet<string>(["UserName"], StringComparer.OrdinalIgnoreCase);

        Assert.Equal("Username2", taken.Claim("Username"));
    }

    [Fact]
    public void Claim_SkipsANumberedNameAlreadyTaken()
    {
        var taken = new HashSet<string>(["value", "value2"], StringComparer.Ordinal);

        Assert.Equal("value3", taken.Claim("value"));
    }

    [Theory]
    [InlineData("TwoFactor", "TwoFactor")]
    [InlineData("_id", "_id")]
    [InlineData("2fa", "Header2fa")]
    [InlineData("", "Header")]
    public void WithIdentifierStart_PrefixesANameThatDoesNotStartAsAnIdentifier(
        string name,
        string expected)
        => Assert.Equal(expected, name.WithIdentifierStart("Header"));
}