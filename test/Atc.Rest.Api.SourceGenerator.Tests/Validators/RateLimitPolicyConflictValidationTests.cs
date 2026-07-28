namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// Tests for Rate Limiting policy-conflict validation (RL003).
/// </summary>
/// <remarks>
/// A policy name is the unit of limiter registration in RateLimiterOptions, so one name maps to
/// exactly one limiter. Declaring the same name at several sites with different settings means the
/// first declaration wins and the rest are silently discarded.
/// </remarks>
[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
[SuppressMessage("", "SA1515:Single-line comment should be preceded by blank line", Justification = "OK")]
public class RateLimitPolicyConflictValidationTests
{
    private const string TestFilePath = "test.yaml";

    // ========== RL003: conflicting settings under one policy name ==========
    [Fact]
    public void Validate_SamePolicyWithConflictingPartition_ReportsRL003()
    {
        // Arrange - the reported case: two endpoints share 'logs-read' but want different partitions.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /logs/device:
                                get:
                                  operationId: getDeviceLogs
                                  x-ratelimit-policy: logs-read
                                  x-ratelimit-partition: user
                                  responses:
                                    '200':
                                      description: OK
                              /logs/workspace:
                                get:
                                  operationId: getWorkspaceLogs
                                  x-ratelimit-policy: logs-read
                                  x-ratelimit-partition: global
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
        var rl003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPolicyConflictingSettings);
        Assert.NotNull(rl003);
        Assert.Equal(Generator.Models.DiagnosticSeverity.Warning, rl003.Severity);
        Assert.Contains("logs-read", rl003.Message, StringComparison.Ordinal);
        Assert.Contains("x-ratelimit-partition", rl003.Message, StringComparison.Ordinal);
        Assert.Contains("getDeviceLogs", rl003.Message, StringComparison.Ordinal);
        Assert.Contains("getWorkspaceLogs", rl003.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SamePolicyWithConflictingPermitLimit_ReportsRL003()
    {
        // Arrange - the rule generalizes to the settings that predate partitioning.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /a:
                                x-ratelimit-policy: shared
                                x-ratelimit-permit-limit: 100
                                get:
                                  operationId: getA
                                  responses:
                                    '200':
                                      description: OK
                              /b:
                                x-ratelimit-policy: shared
                                x-ratelimit-permit-limit: 500
                                get:
                                  operationId: getB
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
        var rl003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPolicyConflictingSettings);
        Assert.NotNull(rl003);
        Assert.Contains("x-ratelimit-permit-limit", rl003.Message, StringComparison.Ordinal);
        Assert.Contains("100", rl003.Message, StringComparison.Ordinal);
        Assert.Contains("500", rl003.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_SamePolicyWithConflictingClaim_ReportsRL003()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /a:
                                x-ratelimit-policy: shared
                                x-ratelimit-partition: user
                                x-ratelimit-partition-claim: sub
                                get:
                                  operationId: getA
                                  responses:
                                    '200':
                                      description: OK
                              /b:
                                x-ratelimit-policy: shared
                                x-ratelimit-partition: user
                                x-ratelimit-partition-claim: oid
                                get:
                                  operationId: getB
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
        var rl003 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPolicyConflictingSettings);
        Assert.NotNull(rl003);
        Assert.Contains("x-ratelimit-partition-claim", rl003.Message, StringComparison.Ordinal);
    }

    // ========== No false positives on idiomatic authoring ==========
    [Fact]
    public void Validate_SamePolicyReDeclaredWithoutRepeatingSettings_NoRL003()
    {
        // Arrange - THE false-positive guard. Re-declaring a policy name on a sub-path without
        // repeating every setting is the idiomatic style throughout this repo's specs and samples
        // ("# Inherit accounts policy"). The second site contradicts nothing, so it must not warn.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: global
                            x-ratelimit-permit-limit: 1000
                            x-ratelimit-window-seconds: 60
                            paths:
                              /orders:
                                x-ratelimit-policy: orders-standard
                                x-ratelimit-permit-limit: 100
                                x-ratelimit-window-seconds: 60
                                x-ratelimit-partition: ip
                                get:
                                  operationId: listOrders
                                  responses:
                                    '200':
                                      description: OK
                              /orders/{orderId}:
                                x-ratelimit-policy: orders-standard
                                x-ratelimit-partition: ip
                                parameters:
                                  - name: orderId
                                    in: path
                                    required: true
                                    schema:
                                      type: string
                                get:
                                  operationId: getOrderById
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitPolicyConflictingSettings));
    }

    [Fact]
    public void Validate_SamePolicyWithIdenticalSettings_NoRL003()
    {
        // Arrange - the pattern the RateLimit scenario uses: identical values at every site.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /a:
                                x-ratelimit-policy: shared
                                x-ratelimit-permit-limit: 100
                                x-ratelimit-partition: ip
                                get:
                                  operationId: getA
                                  responses:
                                    '200':
                                      description: OK
                              /b:
                                x-ratelimit-policy: shared
                                x-ratelimit-permit-limit: 100
                                x-ratelimit-partition: ip
                                get:
                                  operationId: getB
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitPolicyConflictingSettings));
    }

    [Fact]
    public void Validate_SamePolicyWithDifferentlyCasedPartition_NoRL003()
    {
        // Arrange - parsing is case-insensitive, so 'user' and 'User' are the same setting.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /a:
                                x-ratelimit-policy: shared
                                x-ratelimit-partition: user
                                get:
                                  operationId: getA
                                  responses:
                                    '200':
                                      description: OK
                              /b:
                                x-ratelimit-policy: shared
                                x-ratelimit-partition: User
                                get:
                                  operationId: getB
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitPolicyConflictingSettings));
    }

    [Fact]
    public void Validate_DistinctPolicyNamesWithDifferentPartitions_NoRL003()
    {
        // Arrange - the recommended fix for a genuine conflict: split into two policy names.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /logs/device:
                                get:
                                  operationId: getDeviceLogs
                                  x-ratelimit-policy: logs-read-device
                                  x-ratelimit-partition: user
                                  responses:
                                    '200':
                                      description: OK
                              /logs/workspace:
                                get:
                                  operationId: getWorkspaceLogs
                                  x-ratelimit-policy: logs-read-workspace
                                  x-ratelimit-partition: global
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitPolicyConflictingSettings));
    }

    [Fact]
    public void Validate_NoRateLimitExtensions_NoRL003()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /a:
                                get:
                                  operationId: getA
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitPolicyConflictingSettings));
    }

    // ========== Helper Methods ==========
    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;
}