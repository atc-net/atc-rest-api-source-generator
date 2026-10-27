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

    // ========== STREAM002: a request body only as Server-Sent Events or multipart/mixed ==========
    [Theory]
    [InlineData("text/event-stream")]
    [InlineData("multipart/mixed")]
    public void Validate_RequestBodyOnlyAsAnEventStreamOrMultipartMixed_ReportsSTREAM002(
        string mediaType)
    {
        // No generator sends or reads such a body: without the error it would be dropped
        var diagnostics = Validate(RequestBodyYaml($$"""
                                                     {{mediaType}}:
                                                       schema:
                                                         type: object
                                                     """));

        var stream002 = Assert.Single(diagnostics, d => d.RuleId == Generator.RuleIdentifiers.StreamingRequestBodyFramingUnsupported);
        Assert.Equal(Generator.Models.DiagnosticSeverity.Error, stream002.Severity);
        Assert.Contains(mediaType, stream002.Message, StringComparison.Ordinal);
        Assert.Contains("application/jsonl", stream002.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EventStreamRequestBodyBesideJson_NoSTREAM002()
    {
        // The JSON body is sent; the event stream beside it is an alternative nobody needs
        var diagnostics = Validate(RequestBodyYaml("""
                                                   application/json:
                                                     schema:
                                                       type: object
                                                   text/event-stream:
                                                     schema:
                                                       type: object
                                                   """));

        Assert.DoesNotContain(diagnostics, d => d.RuleId == Generator.RuleIdentifiers.StreamingRequestBodyFramingUnsupported);
    }

    // ========== STREAM003: a JSON Lines / JSON Text Sequence request body without a record schema ==========
    [Theory]
    [InlineData("application/jsonl")]
    [InlineData("application/x-ndjson")]
    [InlineData("application/json-seq")]
    public void Validate_StreamedRequestBodyWithoutASchema_ReportsSTREAM003(
        string mediaType)
    {
        var diagnostics = Validate(RequestBodyYaml($$"""
                                                     {{mediaType}}: {}
                                                     """));

        var stream003 = Assert.Single(diagnostics, d => d.RuleId == Generator.RuleIdentifiers.StreamedRequestBodyWithoutRecordSchema);
        Assert.Equal(Generator.Models.DiagnosticSeverity.Error, stream003.Severity);
        Assert.Contains(mediaType, stream003.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("itemSchema")]
    public void Validate_StreamedRequestBodyWithARecordSchema_NoSTREAM003OrSTREAM002(
        string keyword)
    {
        var diagnostics = Validate(
            RequestBodyYaml($$"""
                              application/jsonl:
                                {{keyword}}:
                                  type: object
                              """),
            keyword == "itemSchema" ? "3.2.0" : "3.1.1");

        Assert.DoesNotContain(diagnostics, d => d.RuleId == Generator.RuleIdentifiers.StreamedRequestBodyWithoutRecordSchema);
        Assert.DoesNotContain(diagnostics, d => d.RuleId == Generator.RuleIdentifiers.StreamingRequestBodyFramingUnsupported);
    }

    [Fact]
    public void Validate_WebhookPayloadAsJsonLinesWithoutASchema_ReportsSTREAM003()
    {
        const string yaml = """
                            openapi: 3.1.1
                            info:
                              title: Test API
                              version: 1.0.0
                            paths: {}
                            webhooks:
                              readingsArrived:
                                post:
                                  operationId: onReadingsArrived
                                  requestBody:
                                    content:
                                      application/jsonl: {}
                                  responses:
                                    '200':
                                      description: OK
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, document, [], TestFilePath);

        Assert.Contains(diagnostics, d => d.RuleId == Generator.RuleIdentifiers.StreamedRequestBodyWithoutRecordSchema);
    }

    // ========== Helper Methods ==========
    private static string RequestBodyYaml(string content)
    {
        var indented = string.Join("\n", content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => "              " + line));
        return $$"""
                 openapi: 3.1.1
                 info:
                   title: Test API
                   version: 1.0.0
                 paths:
                   /readings:
                     post:
                       operationId: importReadings
                       requestBody:
                         content:
                 {{indented}}
                       responses:
                         '202':
                           description: Accepted
                 """;
    }

    private static List<Generator.Models.DiagnosticMessage> Validate(
        string yaml,
        string version = "3.1.1")
    {
        var document = ParseYaml(yaml.Replace("openapi: 3.1.1", $"openapi: {version}", StringComparison.Ordinal));
        Assert.NotNull(document);
        return OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, document, [], TestFilePath);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;
}