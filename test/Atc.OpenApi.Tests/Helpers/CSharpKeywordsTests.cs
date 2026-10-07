namespace Atc.OpenApi.Tests.Helpers;

/// <summary>
/// <see cref="CSharpKeywords"/> knows exactly the reserved keywords the compiler knows: a missing one would let a schema
/// named <c>event</c> become a type that does not compile, an extra one would rename a name that needs no change.
/// </summary>
public class CSharpKeywordsTests
{
    public static TheoryData<string> ReservedKeywords
        => [.. SyntaxFacts.GetReservedKeywordKinds().Select(SyntaxFacts.GetText)];

    [Theory]
    [MemberData(nameof(ReservedKeywords))]
    public void IsKeyword_EveryReservedKeyword_IsOne(string keyword)
        => Assert.True(CSharpKeywords.IsKeyword(keyword));

    [Theory]
    [InlineData("Class")]
    [InlineData("record")]
    [InlineData("var")]
    [InlineData("async")]
    [InlineData("value")]
    [InlineData("")]
    [InlineData(null)]
    public void IsKeyword_AContextualKeywordOrAnyOtherName_IsNotOne(
        string? name)
        => Assert.False(CSharpKeywords.IsKeyword(name));
}