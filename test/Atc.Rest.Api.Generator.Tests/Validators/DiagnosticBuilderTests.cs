namespace Atc.Rest.Api.Generator.Tests.Validators;

public class DiagnosticBuilderTests
{
    // ========== SchemaReferenceError Tests ==========
    [Fact]
    public void SchemaReferenceError_ReturnsErrorWithCorrectFields()
    {
        var result = DiagnosticBuilder.SchemaReferenceError(
            "MissingSchema",
            "#/paths/~1pets/get/responses/200",
            "spec.yaml");

        Assert.Equal(DiagnosticSeverity.Error, result.Severity);
        Assert.Contains("MissingSchema", result.Message, StringComparison.Ordinal);
        Assert.Equal("spec.yaml", result.FilePath);
        Assert.NotNull(result.Suggestions);
        Assert.True(result.Suggestions!.Count >= 2);
    }

    // ========== OperationIdCasingWarning Tests ==========
    [Fact]
    public void OperationIdCasingWarning_ReturnsWarningWithSuggestion()
    {
        var result = DiagnosticBuilder.OperationIdCasingWarning(
            "ListPets",
            "listPets",
            "GET",
            "/pets",
            "spec.yaml");

        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains("ListPets", result.Message, StringComparison.Ordinal);
        Assert.Contains("camelCase", result.Message, StringComparison.Ordinal);
        Assert.Contains("GET /pets", result.Context!, StringComparison.Ordinal);
        Assert.NotNull(result.Suggestions);
        Assert.Contains(result.Suggestions!, s => s.Contains("listPets", StringComparison.Ordinal));
    }

    // ========== ParsingError Tests ==========
    [Fact]
    public void ParsingError_WithJsonPointer_IncludesPointerInSuggestions()
    {
        var result = DiagnosticBuilder.ParsingError(
            "Invalid type",
            "#/components/schemas/Pet",
            "spec.yaml");

        Assert.Equal(DiagnosticSeverity.Error, result.Severity);
        Assert.Contains("Invalid type", result.Message, StringComparison.Ordinal);
        Assert.NotNull(result.Suggestions);
        Assert.Contains(result.Suggestions!, s => s.Contains("#/components/schemas/Pet", StringComparison.Ordinal));
    }

    [Fact]
    public void ParsingError_WithoutJsonPointer_NoPointerInSuggestions()
    {
        var result = DiagnosticBuilder.ParsingError(
            "Syntax error",
            null,
            "spec.yaml");

        Assert.Equal(DiagnosticSeverity.Error, result.Severity);
        Assert.NotNull(result.Suggestions);
        Assert.DoesNotContain(result.Suggestions!, s => s.Contains("JSON path", StringComparison.Ordinal));
    }

    // ========== MissingRequiredField Tests ==========
    [Fact]
    public void MissingRequiredField_WithSuggestion_IncludesBoth()
    {
        var result = DiagnosticBuilder.MissingRequiredField(
            "ATC_API_001",
            "operationId",
            "GET /pets operation",
            "#/paths/~1pets/get",
            "spec.yaml",
            "Auto-generated IDs are less readable");

        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Contains("operationId", result.Message, StringComparison.Ordinal);
        Assert.NotNull(result.Suggestions);
        Assert.Equal(2, result.Suggestions!.Count);
    }

    [Fact]
    public void MissingRequiredField_WithoutSuggestion_HasOneSuggestion()
    {
        var result = DiagnosticBuilder.MissingRequiredField(
            "ATC_API_001",
            "operationId",
            "GET /pets operation",
            "#/paths/~1pets/get",
            "spec.yaml");

        Assert.NotNull(result.Suggestions);
        Assert.Single(result.Suggestions!);
    }

    // ========== Documentation URL Tests ==========
    [Fact]
    public void AllBuilders_IncludeDocumentationUrl()
    {
        var error = DiagnosticBuilder.SchemaReferenceError("ref", "path", "file");
        var warning = DiagnosticBuilder.OperationIdCasingWarning("id", "id", "GET", "/", "file");
        var parsing = DiagnosticBuilder.ParsingError("msg", null, "file");
        var missing = DiagnosticBuilder.MissingRequiredField("ATC_001", "field", "ctx", "path", "file");

        Assert.NotNull(error.DocumentationUrl);
        Assert.NotNull(warning.DocumentationUrl);
        Assert.NotNull(parsing.DocumentationUrl);
        Assert.NotNull(missing.DocumentationUrl);
    }
}