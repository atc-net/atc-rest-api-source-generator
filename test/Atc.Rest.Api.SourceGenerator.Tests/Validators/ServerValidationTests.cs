namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

public class ServerValidationTests
{
    private const string TestFilePath = "test.yaml";

    //// ========== Positive Tests (Valid Server URLs - No SRV001) ==========

    [Fact]
    public void Validate_ValidHttpsUrl_NoSRV001()
    {
        // Arrange
        var document = ParseYaml(CreateServerYaml("https://api.example.com"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var srv001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.InvalidServerUrl);
        Assert.Null(srv001);
    }

    [Fact]
    public void Validate_ValidHttpUrl_NoSRV001()
    {
        // Arrange
        var document = ParseYaml(CreateServerYaml("http://localhost:8080"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var srv001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.InvalidServerUrl);
        Assert.Null(srv001);
    }

    [Fact]
    public void Validate_ValidRelativeUrl_NoSRV001()
    {
        // Arrange
        var document = ParseYaml(CreateServerYaml("/api/v1"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var srv001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.InvalidServerUrl);
        Assert.Null(srv001);
    }

    [Fact]
    public void Validate_ValidUrlWithVariables_NoSRV001()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            servers:
              - url: "{protocol}://api.{environment}.example.com"
                variables:
                  protocol:
                    default: https
                  environment:
                    default: prod
            paths: {}
            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var srv001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.InvalidServerUrl);
        Assert.Null(srv001);
    }

    //// ========== Negative Tests (Invalid Server URLs - Reports SRV001) ==========

    [Fact]
    public void Validate_InvalidUrlFormat_ReportsSRV001()
    {
        // Arrange
        var document = ParseYaml(CreateServerYaml("not-a-valid-url"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var srv001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.InvalidServerUrl);
        Assert.NotNull(srv001);
        Assert.Contains("not-a-valid-url", srv001.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_UndefinedVariable_ReportsSRV001()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            servers:
              - url: "https://api.{environment}.example.com"
            paths: {}
            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var srv001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.InvalidServerUrl);
        Assert.NotNull(srv001);
        Assert.Contains("environment", srv001.Message, StringComparison.Ordinal);
        Assert.Contains("not defined", srv001.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NoServers_NoSRV001()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        var srv001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.InvalidServerUrl);
        Assert.Null(srv001);
    }

    [Fact]
    public void Validate_StandardMode_SkipsServerValidation()
    {
        // Arrange
        var document = ParseYaml(CreateServerYaml("not-a-valid-url"));
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Standard,
            document,
            [],
            TestFilePath);

        // Assert - Standard mode should not run strict validation
        var srv001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.InvalidServerUrl);
        Assert.Null(srv001);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;

    private static string CreateServerYaml(string serverUrl)
        => $$"""
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            servers:
              - url: "{{serverUrl}}"
            paths: {}
            """;
}