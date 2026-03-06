namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class DiagnosticMessageFormatterTests
{
    private static DiagnosticMessage CreateTestMessage(
        DiagnosticSeverity severity = DiagnosticSeverity.Warning,
        string? context = null,
        string? filePath = null,
        int? lineNumber = null,
        int? columnNumber = null,
        IReadOnlyList<string>? suggestions = null,
        string? documentationUrl = null)
        => new(
            RuleId: "ATC_API_TEST001",
            Message: "Test diagnostic message",
            Severity: severity,
            FilePath: filePath,
            LineNumber: lineNumber,
            ColumnNumber: columnNumber,
            Context: context,
            Suggestions: suggestions,
            DocumentationUrl: documentationUrl);

    // ========== FormatRich Tests ==========
    [Fact]
    public void FormatRich_MinimalMessage_ContainsRuleIdAndMessage()
    {
        var message = CreateTestMessage();
        var result = DiagnosticMessageFormatter.FormatRich(message);

        Assert.Contains("ATC_API_TEST001", result, StringComparison.Ordinal);
        Assert.Contains("Test diagnostic message", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_WithContext_ContainsContext()
    {
        var message = CreateTestMessage(context: "listPets");
        var result = DiagnosticMessageFormatter.FormatRich(message);

        Assert.Contains("in 'listPets'", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_WithLocation_ContainsFilePath()
    {
        var message = CreateTestMessage(filePath: "spec.yaml", lineNumber: 42, columnNumber: 10);
        var result = DiagnosticMessageFormatter.FormatRich(message);

        Assert.Contains("spec.yaml", result, StringComparison.Ordinal);
        Assert.Contains("line 42", result, StringComparison.Ordinal);
        Assert.Contains("column 10", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_WithSuggestions_ContainsSuggestions()
    {
        var message = CreateTestMessage(suggestions: ["Fix A", "Fix B"]);
        var result = DiagnosticMessageFormatter.FormatRich(message);

        Assert.Contains("1. Fix A", result, StringComparison.Ordinal);
        Assert.Contains("2. Fix B", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_ErrorSeverity_ContainsErrorPrefix()
    {
        var message = CreateTestMessage(severity: DiagnosticSeverity.Error);
        var result = DiagnosticMessageFormatter.FormatRich(message);

        Assert.StartsWith("error", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_WithAtcRuleId_ContainsDocumentationUrl()
    {
        var message = CreateTestMessage();
        var result = DiagnosticMessageFormatter.FormatRich(message);

        Assert.Contains("Documentation:", result, StringComparison.Ordinal);
        Assert.Contains("atc-api-test001", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_WithCustomDocUrl_UsesCustomUrl()
    {
        var message = CreateTestMessage(documentationUrl: "https://example.com/docs");
        var result = DiagnosticMessageFormatter.FormatRich(message);

        Assert.Contains("https://example.com/docs", result, StringComparison.Ordinal);
    }

    // ========== FormatSingleLine Tests ==========
    [Fact]
    public void FormatSingleLine_MinimalMessage_FormatsCorrectly()
    {
        var message = CreateTestMessage();
        var result = DiagnosticMessageFormatter.FormatSingleLine(message);

        Assert.Contains("warning ATC_API_TEST001:", result, StringComparison.Ordinal);
        Assert.Contains("Test diagnostic message", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSingleLine_WithFileAndLine_IncludesLocation()
    {
        var message = CreateTestMessage(filePath: "spec.yaml", lineNumber: 10);
        var result = DiagnosticMessageFormatter.FormatSingleLine(message);

        Assert.StartsWith("spec.yaml(10): ", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSingleLine_WithContext_IncludesContext()
    {
        var message = CreateTestMessage(context: "getPets");
        var result = DiagnosticMessageFormatter.FormatSingleLine(message);

        Assert.Contains("[getPets]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSingleLine_ErrorSeverity_ShowsError()
    {
        var message = CreateTestMessage(severity: DiagnosticSeverity.Error);
        var result = DiagnosticMessageFormatter.FormatSingleLine(message);

        Assert.Contains("error ATC_API_TEST001:", result, StringComparison.Ordinal);
    }

    // ========== FormatSpectreMarkup Tests ==========
    [Fact]
    public void FormatSpectreMarkup_ErrorSeverity_UsesRedColor()
    {
        var message = CreateTestMessage(severity: DiagnosticSeverity.Error);
        var result = DiagnosticMessageFormatter.FormatSpectreMarkup(message);

        Assert.Contains("[red]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSpectreMarkup_WarningSeverity_UsesYellowColor()
    {
        var message = CreateTestMessage(severity: DiagnosticSeverity.Warning);
        var result = DiagnosticMessageFormatter.FormatSpectreMarkup(message);

        Assert.Contains("[yellow]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSpectreMarkup_ContainsBoldRuleId()
    {
        var message = CreateTestMessage();
        var result = DiagnosticMessageFormatter.FormatSpectreMarkup(message);

        Assert.Contains("[bold]ATC_API_TEST001[/]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSpectreMarkup_EscapesBrackets()
    {
        var msg = new DiagnosticMessage(
            RuleId: "TEST",
            Message: "Value [must] be valid",
            Severity: DiagnosticSeverity.Warning);
        var result = DiagnosticMessageFormatter.FormatSpectreMarkup(msg);

        Assert.Contains("Value [[must]] be valid", result, StringComparison.Ordinal);
    }

    // ========== Non-ATC Rule ID Tests ==========
    [Fact]
    public void FormatRich_NonAtcRuleId_NoDocumentationUrl()
    {
        var msg = new DiagnosticMessage(
            RuleId: "CUSTOM_RULE",
            Message: "Custom message",
            Severity: DiagnosticSeverity.Info);
        var result = DiagnosticMessageFormatter.FormatRich(msg);

        Assert.DoesNotContain("Documentation:", result, StringComparison.Ordinal);
    }
}