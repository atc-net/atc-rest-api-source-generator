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
        // Arrange
        var message = CreateTestMessage();

        // Act
        var result = DiagnosticMessageFormatter.FormatRich(message);

        // Assert
        Assert.Contains("ATC_API_TEST001", result, StringComparison.Ordinal);
        Assert.Contains("Test diagnostic message", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_WithContext_ContainsContext()
    {
        // Arrange
        var message = CreateTestMessage(context: "listPets");

        // Act
        var result = DiagnosticMessageFormatter.FormatRich(message);

        // Assert
        Assert.Contains("in 'listPets'", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_WithLocation_ContainsFilePath()
    {
        // Arrange
        var message = CreateTestMessage(filePath: "spec.yaml", lineNumber: 42, columnNumber: 10);

        // Act
        var result = DiagnosticMessageFormatter.FormatRich(message);

        // Assert
        Assert.Contains("spec.yaml", result, StringComparison.Ordinal);
        Assert.Contains("line 42", result, StringComparison.Ordinal);
        Assert.Contains("column 10", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_WithSuggestions_ContainsSuggestions()
    {
        // Arrange
        var message = CreateTestMessage(suggestions: ["Fix A", "Fix B"]);

        // Act
        var result = DiagnosticMessageFormatter.FormatRich(message);

        // Assert
        Assert.Contains("1. Fix A", result, StringComparison.Ordinal);
        Assert.Contains("2. Fix B", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_ErrorSeverity_ContainsErrorPrefix()
    {
        // Arrange
        var message = CreateTestMessage(severity: DiagnosticSeverity.Error);

        // Act
        var result = DiagnosticMessageFormatter.FormatRich(message);

        // Assert
        Assert.StartsWith("error", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_WithAtcRuleId_ContainsDocumentationUrl()
    {
        // Arrange
        var message = CreateTestMessage();

        // Act
        var result = DiagnosticMessageFormatter.FormatRich(message);

        // Assert
        Assert.Contains("Documentation:", result, StringComparison.Ordinal);
        Assert.Contains("atc-api-test001", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatRich_WithCustomDocUrl_UsesCustomUrl()
    {
        // Arrange
        var message = CreateTestMessage(documentationUrl: "https://example.com/docs");

        // Act
        var result = DiagnosticMessageFormatter.FormatRich(message);

        // Assert
        Assert.Contains("https://example.com/docs", result, StringComparison.Ordinal);
    }

    // ========== FormatSingleLine Tests ==========
    [Fact]
    public void FormatSingleLine_MinimalMessage_FormatsCorrectly()
    {
        // Arrange
        var message = CreateTestMessage();

        // Act
        var result = DiagnosticMessageFormatter.FormatSingleLine(message);

        // Assert
        Assert.Contains("warning ATC_API_TEST001:", result, StringComparison.Ordinal);
        Assert.Contains("Test diagnostic message", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSingleLine_WithFileAndLine_IncludesLocation()
    {
        // Arrange
        var message = CreateTestMessage(filePath: "spec.yaml", lineNumber: 10);

        // Act
        var result = DiagnosticMessageFormatter.FormatSingleLine(message);

        // Assert
        Assert.StartsWith("spec.yaml(10): ", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSingleLine_WithContext_IncludesContext()
    {
        // Arrange
        var message = CreateTestMessage(context: "getPets");

        // Act
        var result = DiagnosticMessageFormatter.FormatSingleLine(message);

        // Assert
        Assert.Contains("[getPets]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSingleLine_ErrorSeverity_ShowsError()
    {
        // Arrange
        var message = CreateTestMessage(severity: DiagnosticSeverity.Error);

        // Act
        var result = DiagnosticMessageFormatter.FormatSingleLine(message);

        // Assert
        Assert.Contains("error ATC_API_TEST001:", result, StringComparison.Ordinal);
    }

    // ========== FormatSpectreMarkup Tests ==========
    [Fact]
    public void FormatSpectreMarkup_ErrorSeverity_UsesRedColor()
    {
        // Arrange
        var message = CreateTestMessage(severity: DiagnosticSeverity.Error);

        // Act
        var result = DiagnosticMessageFormatter.FormatSpectreMarkup(message);

        // Assert
        Assert.Contains("[red]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSpectreMarkup_WarningSeverity_UsesYellowColor()
    {
        // Arrange
        var message = CreateTestMessage(severity: DiagnosticSeverity.Warning);

        // Act
        var result = DiagnosticMessageFormatter.FormatSpectreMarkup(message);

        // Assert
        Assert.Contains("[yellow]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSpectreMarkup_ContainsBoldRuleId()
    {
        // Arrange
        var message = CreateTestMessage();

        // Act
        var result = DiagnosticMessageFormatter.FormatSpectreMarkup(message);

        // Assert
        Assert.Contains("[bold]ATC_API_TEST001[/]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatSpectreMarkup_EscapesBrackets()
    {
        // Arrange
        var msg = new DiagnosticMessage(
            RuleId: "TEST",
            Message: "Value [must] be valid",
            Severity: DiagnosticSeverity.Warning);

        // Act
        var result = DiagnosticMessageFormatter.FormatSpectreMarkup(msg);

        // Assert
        Assert.Contains("Value [[must]] be valid", result, StringComparison.Ordinal);
    }

    // ========== Non-ATC Rule ID Tests ==========
    [Fact]
    public void FormatRich_NonAtcRuleId_NoDocumentationUrl()
    {
        // Arrange
        var msg = new DiagnosticMessage(
            RuleId: "CUSTOM_RULE",
            Message: "Custom message",
            Severity: DiagnosticSeverity.Info);

        // Act
        var result = DiagnosticMessageFormatter.FormatRich(msg);

        // Assert
        Assert.DoesNotContain("Documentation:", result, StringComparison.Ordinal);
    }
}