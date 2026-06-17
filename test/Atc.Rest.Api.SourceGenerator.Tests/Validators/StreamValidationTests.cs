namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// Tests for Streaming validation rules (STREAM001).
/// </summary>
[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
[SuppressMessage("", "SA1515:Single-line comment should be preceded by blank line", Justification = "OK")]
public class StreamValidationTests
{
    private const string TestFilePath = "test.yaml";

    // ========== STREAM001: Streaming media type with unsupported prefixEncoding ==========
    [Fact]
    public void Validate_StreamingMediaTypeWithPrefixEncoding_ReportsSTREAM001()
    {
        // Arrange - A3: prefixEncoding on a streaming response media type triggers ATC_API_STREAM001
        const string yaml = """
                            openapi: 3.2.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /events:
                                get:
                                  operationId: streamEvents
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/x-ndjson:
                                          itemSchema:
                                            type: object
                                          prefixEncoding:
                                            - contentType: application/octet-stream
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Standard,
            document,
            [],
            TestFilePath);

        // Assert
        var stream001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.StreamingPrefixEncodingUnsupported);
        Assert.NotNull(stream001);
        Assert.Equal(Generator.Models.DiagnosticSeverity.Info, stream001.Severity);
        Assert.Contains("prefixEncoding", stream001.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_StreamingMediaTypeWithoutPrefixEncoding_NoSTREAM001()
    {
        // Arrange - streaming media type without prefixEncoding must NOT trigger STREAM001
        const string yaml = """
                            openapi: 3.2.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /events:
                                get:
                                  operationId: streamEvents
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/x-ndjson:
                                          itemSchema:
                                            type: object
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Standard,
            document,
            [],
            TestFilePath);

        // Assert
        var stream001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.StreamingPrefixEncodingUnsupported);
        Assert.Null(stream001);
    }

    // ========== Helper Methods ==========
    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;
}