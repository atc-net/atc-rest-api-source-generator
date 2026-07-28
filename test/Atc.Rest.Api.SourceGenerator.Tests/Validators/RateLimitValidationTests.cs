namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// Tests for Rate Limiting validation rules (RL001, RL002).
/// </summary>
[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
[SuppressMessage("", "SA1515:Single-line comment should be preceded by blank line", Justification = "OK")]
public class RateLimitValidationTests
{
    private const string TestFilePath = "test.yaml";

    // ========== RL001: Unrecognized x-ratelimit-partition value ==========
    [Theory]
    [InlineData("users")]
    [InlineData("ip-address")]
    [InlineData("per-user")]
    [InlineData("none")]
    public void Validate_UnrecognizedPartitionValue_ReportsRL001(
        string partitionValue)
    {
        // Arrange - an unrecognized partition value silently falls back to 'global', which means one
        // shared bucket for every caller. That is the failure mode partitioning exists to prevent,
        // so it must not be silent.
        var yaml = $"""
                    openapi: 3.0.0
                    info:
                      title: Test API
                      version: 1.0.0
                    x-ratelimit-policy: global
                    x-ratelimit-partition: {partitionValue}
                    paths:
                      /pets:
                        get:
                          operationId: getPets
                          responses:
                            '200':
                              description: OK
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
        var rl001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPartitionValueUnrecognized);
        Assert.NotNull(rl001);
        Assert.Equal(Generator.Models.DiagnosticSeverity.Warning, rl001.Severity);
        Assert.Contains(partitionValue, rl001.Message, StringComparison.Ordinal);
        Assert.Contains("global", rl001.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("global")]
    [InlineData("ip")]
    [InlineData("user")]
    // Parsing is case-insensitive (ParsePartitionStrategy lowercases), so these are valid too.
    [InlineData("User")]
    [InlineData("IP")]
    public void Validate_RecognizedPartitionValue_NoRL001(string partitionValue)
    {
        // Arrange
        var yaml = $"""
                    openapi: 3.0.0
                    info:
                      title: Test API
                      version: 1.0.0
                    x-ratelimit-policy: global
                    x-ratelimit-partition: {partitionValue}
                    paths:
                      /pets:
                        get:
                          operationId: getPets
                          responses:
                            '200':
                              description: OK
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
        Assert.Null(diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPartitionValueUnrecognized));
    }

    [Fact]
    public void Validate_UnrecognizedPartitionValueAtOperationLevel_ReportsRL001()
    {
        // Arrange - the extension is valid at document, path and operation level, so validation must
        // walk all three.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                x-ratelimit-policy: pets
                                get:
                                  operationId: getPets
                                  x-ratelimit-partition: bogus
                                  responses:
                                    '200':
                                      description: OK
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
        var rl001 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPartitionValueUnrecognized);
        Assert.NotNull(rl001);
        Assert.Contains("bogus", rl001.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_UnrecognizedPartitionValueAtPathLevel_ReportsRL001()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                x-ratelimit-policy: pets
                                x-ratelimit-partition: bogus
                                get:
                                  operationId: getPets
                                  responses:
                                    '200':
                                      description: OK
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
        Assert.NotNull(diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPartitionValueUnrecognized));
    }

    [Fact]
    public void Validate_NoRateLimitExtensions_NoRL001OrRL002()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                get:
                                  operationId: getPets
                                  responses:
                                    '200':
                                      description: OK
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
        Assert.Null(diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPartitionValueUnrecognized));
        Assert.Null(diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPartitionClaimWithoutUserPartition));
    }

    // ========== RL002: partition-claim without partition: user ==========
    [Fact]
    public void Validate_PartitionClaimWithoutPartition_ReportsRL002()
    {
        // Arrange - x-ratelimit-partition-claim is only read when the partition is 'user'; with no
        // partition declared at all the claim is silently ignored.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: global
                            x-ratelimit-partition-claim: oid
                            paths:
                              /pets:
                                get:
                                  operationId: getPets
                                  responses:
                                    '200':
                                      description: OK
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
        var rl002 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPartitionClaimWithoutUserPartition);
        Assert.NotNull(rl002);
        Assert.Equal(Generator.Models.DiagnosticSeverity.Warning, rl002.Severity);
        Assert.Contains("x-ratelimit-partition-claim", rl002.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PartitionClaimWithIpPartition_ReportsRL002()
    {
        // Arrange - claim is ignored for ip partitioning too.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: global
                            x-ratelimit-partition: ip
                            x-ratelimit-partition-claim: oid
                            paths:
                              /pets:
                                get:
                                  operationId: getPets
                                  responses:
                                    '200':
                                      description: OK
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
        Assert.NotNull(diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPartitionClaimWithoutUserPartition));
    }

    [Fact]
    public void Validate_PartitionClaimWithUserPartition_NoRL002()
    {
        // Arrange - the supported combination.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: global
                            x-ratelimit-partition: user
                            x-ratelimit-partition-claim: oid
                            paths:
                              /pets:
                                get:
                                  operationId: getPets
                                  responses:
                                    '200':
                                      description: OK
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
        Assert.Null(diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPartitionClaimWithoutUserPartition));
    }

    [Fact]
    public void Validate_PartitionClaimInheritsUserPartitionFromPath_NoRL002()
    {
        // Arrange - the claim is declared on the operation while 'user' comes from the path level.
        // Inheritance means this IS effective, so it must not warn.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /pets:
                                x-ratelimit-policy: pets
                                x-ratelimit-partition: user
                                get:
                                  operationId: getPets
                                  x-ratelimit-partition-claim: oid
                                  responses:
                                    '200':
                                      description: OK
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
        Assert.Null(diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPartitionClaimWithoutUserPartition));
    }

    // ========== Helper Methods ==========
    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;
}