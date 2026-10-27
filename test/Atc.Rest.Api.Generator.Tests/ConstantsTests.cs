namespace Atc.Rest.Api.Generator.Tests;

public class ConstantsTests
{
    [Theory]
    [InlineData("ATC_API_VER001", "https://github.com/atc-net/atc-rest-api-source-generator/wiki/Analyzer-Rules#atc_api_ver001")]
    [InlineData("atc_api_opr031", "https://github.com/atc-net/atc-rest-api-source-generator/wiki/Analyzer-Rules#atc_api_opr031")]
    public void GetRuleUrl_RuleId_LowerCasesTheAnchorAndKeepsUnderscores(
        string ruleId,
        string expected)
    {
        // GitHub lower-cases a heading anchor and leaves underscores alone, so the id is only lower-cased.
        var result = Constants.Documentation.GetRuleUrl(ruleId);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetRuleUrl_NullRuleId_PointsAtTheRulesPage()
    {
        var result = Constants.Documentation.GetRuleUrl(null!);

        Assert.Equal(Constants.Documentation.AnalyzerRulesUrl, result);
        Assert.Equal("https://github.com/atc-net/atc-rest-api-source-generator/wiki/Analyzer-Rules", result);
    }
}