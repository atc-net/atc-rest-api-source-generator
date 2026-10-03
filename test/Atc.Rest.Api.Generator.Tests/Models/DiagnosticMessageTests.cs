namespace Atc.Rest.Api.Generator.Tests.Models;

public class DiagnosticMessageTests
{
    [Fact]
    public void Error_RuleIdAndMessage_IsAnErrorWithoutLocation()
    {
        var diagnostic = DiagnosticMessage.Error("ATC_API_GEN001", "Something broke");

        Assert.Equal("ATC_API_GEN001", diagnostic.RuleId);
        Assert.Equal("Something broke", diagnostic.Message);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Null(diagnostic.FilePath);
        Assert.Null(diagnostic.LineNumber);
        Assert.Null(diagnostic.ColumnNumber);
        Assert.Null(diagnostic.Context);
        Assert.Null(diagnostic.Suggestions);
        Assert.Null(diagnostic.DocumentationUrl);
    }

    [Fact]
    public void Error_WithFilePathAndLine_CarriesTheLocation()
    {
        var diagnostic = DiagnosticMessage.Error("ATC_API_GEN001", "Something broke", "spec.yaml", 42);

        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("spec.yaml", diagnostic.FilePath);
        Assert.Equal(42, diagnostic.LineNumber);
        Assert.Null(diagnostic.ColumnNumber);
    }

    [Fact]
    public void Warning_WithFilePathAndLine_IsAWarningAtThatLocation()
    {
        var diagnostic = DiagnosticMessage.Warning("ATC_API_NAM001", "Use camelCase", "spec.yaml", 7);

        Assert.Equal("ATC_API_NAM001", diagnostic.RuleId);
        Assert.Equal("Use camelCase", diagnostic.Message);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("spec.yaml", diagnostic.FilePath);
        Assert.Equal(7, diagnostic.LineNumber);
    }

    [Fact]
    public void Warning_WithoutLocation_LeavesFilePathAndLineNull()
    {
        var diagnostic = DiagnosticMessage.Warning("ATC_API_NAM001", "Use camelCase");

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Null(diagnostic.FilePath);
        Assert.Null(diagnostic.LineNumber);
    }

    [Fact]
    public void Info_WithFilePath_IsInfoWithoutALineNumber()
    {
        var diagnostic = DiagnosticMessage.Info("ATC_API_MPT001", "Merged 2 part file(s)", "MyApi.yaml");

        Assert.Equal("ATC_API_MPT001", diagnostic.RuleId);
        Assert.Equal("Merged 2 part file(s)", diagnostic.Message);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal("MyApi.yaml", diagnostic.FilePath);
        Assert.Null(diagnostic.LineNumber);
    }

    [Fact]
    public void WithLocation_FilePathLineAndColumn_SetsAllThree()
    {
        var original = DiagnosticMessage.Error("ATC_API_GEN001", "Something broke");

        var located = original.WithLocation("spec.yaml", 12, 5);

        Assert.Equal("spec.yaml", located.FilePath);
        Assert.Equal(12, located.LineNumber);
        Assert.Equal(5, located.ColumnNumber);
        Assert.Equal("ATC_API_GEN001", located.RuleId);
        Assert.Equal("Something broke", located.Message);
        Assert.Equal(DiagnosticSeverity.Error, located.Severity);
    }

    [Fact]
    public void WithLocation_IsNonDestructive_TheOriginalKeepsNoLocation()
    {
        var original = DiagnosticMessage.Warning("ATC_API_NAM001", "Use camelCase", "old.yaml", 1);

        var located = original.WithLocation("new.yaml", 3);

        Assert.Equal("old.yaml", original.FilePath);
        Assert.Equal(1, original.LineNumber);
        Assert.Equal("new.yaml", located.FilePath);
        Assert.Equal(3, located.LineNumber);
        Assert.Null(located.ColumnNumber);
    }

    [Fact]
    public void WithLocation_KeepsContextSuggestionsAndDocumentation()
    {
        var original = new DiagnosticMessage(
            "ATC_API_SCH013",
            "Schema 'Pet' not found",
            DiagnosticSeverity.Error,
            Context: "#/components/schemas/Pet",
            Suggestions: ["Add the schema"],
            DocumentationUrl: "https://example.org/rules#atc_api_sch013");

        var located = original.WithLocation("spec.yaml", 20, 3);

        Assert.Equal("#/components/schemas/Pet", located.Context);
        Assert.Equal(["Add the schema"], located.Suggestions, StringComparer.Ordinal);
        Assert.Equal("https://example.org/rules#atc_api_sch013", located.DocumentationUrl);
    }
}