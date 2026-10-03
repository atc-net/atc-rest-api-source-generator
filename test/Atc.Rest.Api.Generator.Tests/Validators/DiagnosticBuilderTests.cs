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
        Assert.True(result.Suggestions.Count >= 2);
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
        Assert.Contains("GET /pets", result.Context, StringComparison.Ordinal);
        Assert.NotNull(result.Suggestions);
        Assert.Contains(result.Suggestions, s => s.Contains("listPets", StringComparison.Ordinal));
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
        Assert.Contains(result.Suggestions, s => s.Contains("#/components/schemas/Pet", StringComparison.Ordinal));
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
        Assert.DoesNotContain(result.Suggestions, s => s.Contains("JSON path", StringComparison.Ordinal));
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
        Assert.Equal(2, result.Suggestions.Count);
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
        Assert.Single(result.Suggestions);
    }

    // ========== NamingConventionWarning Tests ==========
    [Fact]
    public void NamingConventionWarning_ReturnsWarningWithRenameSuggestionAtTheJsonPath()
    {
        var result = DiagnosticBuilder.NamingConventionWarning(
            RuleIdentifiers.ParameterNameMustBeCamelCase,
            "Parameter",
            "PetId",
            "camelCase",
            "petId",
            "#/paths/~1pets~1{PetId}/get/parameters/0",
            "spec.yaml");

        Assert.Equal(RuleIdentifiers.ParameterNameMustBeCamelCase, result.RuleId);
        Assert.Equal("Parameter 'PetId' must use camelCase", result.Message);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Equal("spec.yaml", result.FilePath);
        Assert.Equal("#/paths/~1pets~1{PetId}/get/parameters/0", result.Context);
        Assert.Equal(["Rename to 'petId'"], result.Suggestions, StringComparer.Ordinal);
        Assert.Equal(Constants.Documentation.GetRuleUrl(RuleIdentifiers.ParameterNameMustBeCamelCase), result.DocumentationUrl);
    }

    [Fact]
    public void NamingConventionWarning_UsesTheGivenRuleIdForTheDocumentationUrl()
    {
        var result = DiagnosticBuilder.NamingConventionWarning(
            RuleIdentifiers.ModelNameMustBePascalCase,
            "Model",
            "pet_dto",
            "PascalCase",
            "PetDto",
            "#/components/schemas/pet_dto",
            "spec.yaml");

        Assert.Equal("Model 'pet_dto' must use PascalCase", result.Message);
        Assert.EndsWith("#atc_api_nam002", result.DocumentationUrl, StringComparison.Ordinal);
    }

    // ========== MissingOperationIdWarning Tests ==========
    [Fact]
    public void MissingOperationIdWarning_NamesTheSyntheticClassAndSuggestsAnOperationId()
    {
        var result = DiagnosticBuilder.MissingOperationIdWarning(
            "get",
            "/pets/{petId}",
            "GetPetsPetId",
            "getPetId",
            "spec.yaml");

        Assert.Equal(RuleIdentifiers.OperationIdMissing, result.RuleId);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Equal(
            "Operation 'GET /pets/{petId}' is missing 'operationId'. " +
            "The generator will use the auto-generated name 'GetPetsPetId'. " +
            "This may produce long or unreadable class names.",
            result.Message);
        Assert.Equal("GET /pets/{petId}", result.Context);
        Assert.Equal("spec.yaml", result.FilePath);
        Assert.NotNull(result.Suggestions);
        Assert.Equal(2, result.Suggestions.Count);
        Assert.Equal("Add 'operationId: getPetId' to this operation", result.Suggestions[0]);
        Assert.Equal(Constants.Documentation.GetRuleUrl(RuleIdentifiers.OperationIdMissing), result.DocumentationUrl);
    }

    // ========== ParameterSerializationNotSupportedWarning Tests ==========
    [Fact]
    public void ParameterSerializationNotSupportedWarning_DescribesTheStyleAndTheFallback()
    {
        var result = DiagnosticBuilder.ParameterSerializationNotSupportedWarning(
            "tags",
            "style 'spaceDelimited' explode=true on array",
            "spec.yaml");

        Assert.Equal(RuleIdentifiers.ParameterSerializationNotSupported, result.RuleId);
        Assert.Equal(DiagnosticSeverity.Warning, result.Severity);
        Assert.Equal(
            "Parameter 'tags' declares style 'spaceDelimited' explode=true on array, which is not yet supported; emitting default form serialization.",
            result.Message);
        Assert.Equal("spec.yaml", result.FilePath);
        Assert.Null(result.Context);
        Assert.NotNull(result.Suggestions);
        var suggestion = Assert.Single(result.Suggestions);
        Assert.Contains("style 'form'", suggestion, StringComparison.Ordinal);
        Assert.Equal(Constants.Documentation.GetRuleUrl(RuleIdentifiers.ParameterSerializationNotSupported), result.DocumentationUrl);
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