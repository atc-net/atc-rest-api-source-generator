namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

public class PathValidationTests
{
    private const string TestFilePath = "test.yaml";

    //// ========== Positive Tests (Valid Paths - No PTH001) ==========

    [Fact]
    public void Validate_ValidPathWithParameter_NoPTH001()
    {
        // Arrange
        var document = ParseYaml(CreatePathYaml("/pets/{petId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var pth001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParametersNotWellFormatted);
        Assert.Null(pth001);
    }

    [Fact]
    public void Validate_ValidPathWithMultipleParameters_NoPTH001()
    {
        // Arrange
        var document = ParseYaml(CreatePathYaml("/users/{userId}/orders/{orderId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var pth001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParametersNotWellFormatted);
        Assert.Null(pth001);
    }

    [Fact]
    public void Validate_ValidPathWithNoParameters_NoPTH001()
    {
        // Arrange
        var document = ParseYaml(CreatePathYaml("/health"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var pth001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParametersNotWellFormatted);
        Assert.Null(pth001);
    }

    [Fact]
    public void Validate_ValidPathWithUnderscoreParameter_NoPTH001()
    {
        // Arrange
        var document = ParseYaml(CreatePathYaml("/pets/{pet_id}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var pth001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParametersNotWellFormatted);
        Assert.Null(pth001);
    }

    //// ========== Negative Tests (Invalid Paths - Reports PTH001) ==========

    [Fact]
    public void Validate_UnbalancedOpenBrace_ReportsPTH001()
    {
        // Arrange
        var document = ParseYaml(CreatePathYaml("/pets/{petId"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var pth001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParametersNotWellFormatted);
        Assert.NotNull(pth001);
        Assert.Contains("unbalanced", pth001.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_UnbalancedCloseBrace_ReportsPTH001()
    {
        // Arrange
        var document = ParseYaml(CreatePathYaml("/pets/petId}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var pth001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParametersNotWellFormatted);
        Assert.NotNull(pth001);
        Assert.Contains("unbalanced", pth001.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_EmptyParameter_ReportsPTH001()
    {
        // Arrange
        var document = ParseYaml(CreatePathYaml("/pets/{}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var pth001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParametersNotWellFormatted);
        Assert.NotNull(pth001);
        Assert.Contains("empty", pth001.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_NestedBraces_ReportsPTH001()
    {
        // Arrange
        var document = ParseYaml(CreatePathYaml("/pets/{{petId}}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var pth001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParametersNotWellFormatted);
        Assert.NotNull(pth001);
        Assert.Contains("nested", pth001.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ParameterWithWhitespace_ReportsPTH001()
    {
        // Arrange
        var document = ParseYaml(CreatePathYaml("/pets/{pet id}"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var pth001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParametersNotWellFormatted);
        Assert.NotNull(pth001);
        Assert.Contains("whitespace", pth001.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_StandardMode_SkipsPathValidation()
    {
        // Arrange
        var document = ParseYaml(CreatePathYaml("/pets/{petId"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Standard,
            document,
            [],
            TestFilePath);

        // Assert - Standard mode should not run strict validation
        var pth001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.PathParametersNotWellFormatted);
        Assert.Null(pth001);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;

    private static string CreatePathYaml(string path)
        => $$"""
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              "{{path}}":
                get:
                  operationId: getResource
                  responses:
                    '200':
                      description: Success
            """;
}