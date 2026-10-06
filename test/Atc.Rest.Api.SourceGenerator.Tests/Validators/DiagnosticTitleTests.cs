namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// Every rule the generators report carries a readable title from its category - never the raw rule id, which
/// is what CACHE, CLT, MPT, RL, STREAM, VER and WBH diagnostics used to show in the error list.
/// </summary>
public sealed class DiagnosticTitleTests
{
    [Fact]
    public void EveryRule_HasATitleOfItsCategory()
    {
        var untitled = RuleCoverageTests.GetAllRuleIdentifiers()
            .Where(id => string.Equals(TitleOf(id), id, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(TitleOf(id)))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.True(untitled.Count == 0, "Rules whose title is the raw id:\n" + string.Join("\n", untitled));
    }

    [Theory]
    [InlineData("ATC_API_GEN001", "Generation Error")]
    [InlineData("ATC_API_NAM001", "Naming Convention")]
    [InlineData("ATC_API_OPR001", "Operation Validation")]
    [InlineData("ATC_API_RL001", "Rate Limiting")]
    [InlineData("ATC_API_CACHE001", "Caching")]
    [InlineData("ATC_API_CLT001", "Client Configuration")]
    [InlineData("ATC_API_MPT001", "Multi-Part Specification")]
    [InlineData("ATC_API_STREAM001", "Streaming")]
    [InlineData("ATC_API_VER001", "Specification Version")]
    [InlineData("ATC_API_WBH001", "Webhook")]
    public void Title_IsTheCategoryName(
        string ruleId,
        string expectedTitle)
        => Assert.Equal(expectedTitle, TitleOf(ruleId));

    private static string TitleOf(string ruleId)
        => DiagnosticHelpers
            .ToRoslynDiagnostic(new Generator.Models.DiagnosticMessage(ruleId, "message", Generator.Models.DiagnosticSeverity.Warning))
            .Descriptor
            .Title
            .ToString(CultureInfo.InvariantCulture);
}