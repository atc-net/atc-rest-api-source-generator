namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class CSharpIdentifierHelperTests
{
    [Theory]
    [InlineData("public", "@public")]
    [InlineData("event", "@event")]
    [InlineData("class", "@class")]
    [InlineData("string", "@string")]
    [InlineData("pets", "pets")]
    [InlineData("Public", "Public")]
    [InlineData("var", "var")]
    public void EscapeIfKeyword_ReturnsExpectedResult(
        string identifier,
        string expected)
    {
        // Only reserved keywords get the @ prefix; a contextual keyword such as var is a valid identifier
        Assert.Equal(expected, CSharpIdentifierHelper.EscapeIfKeyword(identifier));
    }

    // Roslyn's own list, the undocumented __arglist family included
    public static TheoryData<string> ReservedKeywords
        => [.. SyntaxFacts.GetReservedKeywordKinds().Select(SyntaxFacts.GetText)];

    [Theory]
    [MemberData(nameof(ReservedKeywords))]
    public void EscapeIfKeyword_EveryReservedKeyword_IsPrefixed(string keyword)
        => Assert.Equal("@" + keyword, CSharpIdentifierHelper.EscapeIfKeyword(keyword));
}