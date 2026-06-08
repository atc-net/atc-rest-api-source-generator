namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

public class OpenApiVersionValidationTests
{
    private const string TestFilePath = "test.yaml";

    [Fact]
    public void Validate_OpenApi32Document_NoOpenApi20NotSupported()
    {
        // Arrange
        var document = ParseYaml("""
            openapi: 3.2.0
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            """);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        Assert.DoesNotContain(diagnostics, d => d.RuleId == Generator.RuleIdentifiers.OpenApi20NotSupported);
    }

    [Fact]
    public void Validate_Spec3xWithApiVersion2_NoOpenApi20NotSupported()
    {
        // Arrange - info.version is the API version (2.0.0), NOT the OpenAPI spec version.
        var document = ParseYaml("""
            openapi: 3.1.0
            info:
              title: Test API
              version: 2.0.0
            paths: {}
            """);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        Assert.DoesNotContain(diagnostics, d => d.RuleId == Generator.RuleIdentifiers.OpenApi20NotSupported);
    }

    [Fact]
    public void Validate_Swagger20Document_ReportsOpenApi20NotSupported()
    {
        // Arrange - a real Swagger/OpenAPI 2.0 document.
        var document = ParseYaml("""
            swagger: "2.0"
            info:
              title: Test API
              version: 1.0.0
            paths: {}
            """);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict,
            document,
            [],
            TestFilePath);

        // Assert
        Assert.Contains(diagnostics, d => d.RuleId == Generator.RuleIdentifiers.OpenApi20NotSupported);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;
}