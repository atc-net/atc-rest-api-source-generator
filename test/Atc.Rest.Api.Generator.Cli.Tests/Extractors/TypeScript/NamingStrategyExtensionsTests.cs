namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class NamingStrategyExtensionsTests
{
    [Theory]
    [InlineData("user_name", "userName")]
    [InlineData("UserName", "userName")]
    [InlineData("user-name", "userName")]
    [InlineData("user", "user")]
    public void ApplyNamingStrategy_CamelCase_LowerCasesFirstWord(
        string input,
        string expected)
    {
        Assert.Equal(expected, input.ApplyNamingStrategy(TypeScriptNamingStrategy.CamelCase));
    }

    [Theory]
    [InlineData("user_name", "UserName")]
    [InlineData("userName", "UserName")]
    [InlineData("user-name", "UserName")]
    public void ApplyNamingStrategy_PascalCase_UpperCasesFirstLetter(
        string input,
        string expected)
    {
        Assert.Equal(expected, input.ApplyNamingStrategy(TypeScriptNamingStrategy.PascalCase));
    }

    [Theory]
    [InlineData("user_name")]
    [InlineData("UserName")]
    [InlineData("user-name")]
    [InlineData("anything-AT-ALL_42")]
    public void ApplyNamingStrategy_Original_ReturnsInputVerbatim(string input)
    {
        Assert.Equal(input, input.ApplyNamingStrategy(TypeScriptNamingStrategy.Original));
    }

    [Fact]
    public void ApplyNamingStrategy_UnknownStrategy_FallsBackToCamelCase()
    {
        // Defensive: any future-added strategy member should not silently produce
        // unexpected output — the default branch yields CamelCase.
        const TypeScriptNamingStrategy unknown = (TypeScriptNamingStrategy)999;

        Assert.Equal("userName", "user_name".ApplyNamingStrategy(unknown));
    }
}