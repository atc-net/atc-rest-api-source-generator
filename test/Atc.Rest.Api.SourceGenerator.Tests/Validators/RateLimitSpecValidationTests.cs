namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// Tests for the rate limiting spec-authoring guards RL004-RL008.
/// </summary>
/// <remarks>
/// These rules catch <c>x-ratelimit-*</c> configuration that parses fine but breaks the build,
/// crashes the app at startup, or is silently ignored at runtime.
/// </remarks>
[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
[SuppressMessage("", "SA1515:Single-line comment should be preceded by blank line", Justification = "OK")]
public class RateLimitSpecValidationTests
{
    private const string TestFilePath = "test.yaml";

    // ========== RL004: policy names colliding on the generated constant ==========
    [Theory]
    [InlineData("logs-read", "logs_read")]
    [InlineData("logs-read", "logs read")]
    [InlineData("LogsRead", "logs-read")]
    public void Validate_PolicyNamesCollidingOnConstant_ReportsRL004(
        string firstPolicy,
        string secondPolicy)
    {
        // Arrange - ToConstantName splits on '-', '_', ':' and ' ' then PascalCases, so these all
        // collapse to the same C# identifier and emit duplicate consts (CS0102) in RateLimitPolicies.
        var yaml = $"""
                    openapi: 3.0.0
                    info:
                      title: Test API
                      version: 1.0.0
                    paths:
                      /a:
                        get:
                          operationId: getA
                          x-ratelimit-policy: {firstPolicy}
                          responses:
                            '200':
                              description: OK
                      /b:
                        get:
                          operationId: getB
                          x-ratelimit-policy: {secondPolicy}
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
        var rl004 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitPolicyNameCollision);
        Assert.NotNull(rl004);
        Assert.Equal(Generator.Models.DiagnosticSeverity.Error, rl004.Severity);
        Assert.Contains(firstPolicy, rl004.Message, StringComparison.Ordinal);
        Assert.Contains(secondPolicy, rl004.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DistinctPolicyNamesWithDistinctConstants_NoRL004()
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
                                  x-ratelimit-policy: logs-read-device
                                  responses:
                                    '200':
                                      description: OK
                              /b:
                                get:
                                  operationId: getB
                                  x-ratelimit-policy: logs-read-workspace
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitPolicyNameCollision));
    }

    [Fact]
    public void Validate_SamePolicyNameAtSeveralSites_NoRL004()
    {
        // Arrange - the same name repeated is one policy, not a collision.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /a:
                                x-ratelimit-policy: shared
                                get:
                                  operationId: getA
                                  responses:
                                    '200':
                                      description: OK
                              /b:
                                x-ratelimit-policy: shared
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitPolicyNameCollision));
    }

    // ========== RL005: values the limiter constructor rejects at startup ==========
    [Theory]
    [InlineData("x-ratelimit-permit-limit: 0")]
    [InlineData("x-ratelimit-permit-limit: -5")]
    [InlineData("x-ratelimit-window-seconds: 0")]
    [InlineData("x-ratelimit-window-seconds: -1")]
    [InlineData("x-ratelimit-queue-limit: -1")]
    public void Validate_InvalidNumericValue_ReportsRL005(string extensionLine)
    {
        // Arrange - each of these makes the limiter constructor throw ArgumentException inside
        // AddApiRateLimiting, so the app fails to start.
        var yaml = $"""
                    openapi: 3.0.0
                    info:
                      title: Test API
                      version: 1.0.0
                    x-ratelimit-policy: global
                    {extensionLine}
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
        var rl005 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitValueOutOfRange);
        Assert.NotNull(rl005);
        Assert.Equal(Generator.Models.DiagnosticSeverity.Warning, rl005.Severity);
    }

    [Fact]
    public void Validate_ZeroPermitLimit_SuggestsEnabledFalse()
    {
        // Arrange - permit-limit: 0 is a plausible "turn it off" attempt; point at the real switch.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: global
                            x-ratelimit-permit-limit: 0
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
        var rl005 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitValueOutOfRange);
        Assert.NotNull(rl005);
        Assert.Contains("x-ratelimit-enabled", rl005.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ValidNumericValues_NoRL005()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: global
                            x-ratelimit-permit-limit: 100
                            x-ratelimit-window-seconds: 60
                            x-ratelimit-queue-limit: 0
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitValueOutOfRange));
    }

    [Fact]
    public void Validate_NonPositiveWindowOnConcurrency_NoRL005ButReportsRL008()
    {
        // Arrange - concurrency ignores the window entirely, so a bad window cannot crash it.
        // RL008 covers the "this line does nothing" case instead.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: exports
                            x-ratelimit-algorithm: concurrency
                            x-ratelimit-window-seconds: 0
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitValueOutOfRange));
        Assert.NotNull(diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitWindowIgnoredForConcurrency));
    }

    // ========== RL006: x-ratelimit-enabled honoured only at operation level ==========
    [Fact]
    public void Validate_EnabledFalseAtPathLevel_ReportsRL006()
    {
        // Arrange - ExtractRateLimitConfiguration reads 'enabled' from the operation only, so this
        // path is still rate limited despite the author disabling it.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: global
                            paths:
                              /webhooks:
                                x-ratelimit-enabled: false
                                post:
                                  operationId: receiveWebhook
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
        var rl006 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitEnabledIgnoredOutsideOperation);
        Assert.NotNull(rl006);
        Assert.Equal(Generator.Models.DiagnosticSeverity.Warning, rl006.Severity);
        Assert.Contains("x-ratelimit-enabled", rl006.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EnabledFalseAtDocumentLevel_ReportsRL006()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: global
                            x-ratelimit-enabled: false
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
        Assert.NotNull(diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitEnabledIgnoredOutsideOperation));
    }

    [Fact]
    public void Validate_EnabledFalseAtOperationLevel_NoRL006()
    {
        // Arrange - the supported placement.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: global
                            paths:
                              /webhooks:
                                post:
                                  operationId: receiveWebhook
                                  x-ratelimit-enabled: false
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitEnabledIgnoredOutsideOperation));
    }

    // ========== RL007: algorithms that cannot populate Retry-After ==========
    [Theory]
    [InlineData("sliding")]
    [InlineData("concurrency")]
    public void Validate_RetryAfterOnAlgorithmWithoutValue_ReportsRL007(
        string algorithm)
    {
        // Arrange - verified against the runtime: a rejected sliding-window lease advertises
        // RETRY_AFTER but TryGetMetadata returns false, and concurrency never lists it. The
        // generated OnRejected therefore writes no header even though the flag defaults to true.
        var yaml = $"""
                    openapi: 3.0.0
                    info:
                      title: Test API
                      version: 1.0.0
                    x-ratelimit-policy: reports
                    x-ratelimit-algorithm: {algorithm}
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
        var rl007 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitRetryAfterUnsupportedByAlgorithm);
        Assert.NotNull(rl007);
        Assert.Equal(Generator.Models.DiagnosticSeverity.Info, rl007.Severity);
        Assert.Contains(algorithm, rl007.Message, StringComparison.Ordinal);
        Assert.Contains("reports", rl007.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fixed")]
    [InlineData("token-bucket")]
    public void Validate_RetryAfterOnSupportingAlgorithm_NoRL007(
        string algorithm)
    {
        // Arrange - both of these do carry a Retry-After value on a rejected lease.
        var yaml = $"""
                    openapi: 3.0.0
                    info:
                      title: Test API
                      version: 1.0.0
                    x-ratelimit-policy: reports
                    x-ratelimit-algorithm: {algorithm}
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitRetryAfterUnsupportedByAlgorithm));
    }

    [Fact]
    public void Validate_SlidingWithRetryAfterDisabled_NoRL007()
    {
        // Arrange - opting out means nothing is expected, so there is nothing to report.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: reports
                            x-ratelimit-algorithm: sliding
                            x-ratelimit-emit-retry-after: false
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitRetryAfterUnsupportedByAlgorithm));
    }

    [Fact]
    public void Validate_TwoOperationsSharingSlidingPolicy_ReportsRL007Once()
    {
        // Arrange - one diagnostic per policy, not per operation.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /a:
                                x-ratelimit-policy: reports
                                x-ratelimit-algorithm: sliding
                                get:
                                  operationId: getA
                                  responses:
                                    '200':
                                      description: OK
                                post:
                                  operationId: postA
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
        Assert.Single(
            diagnostics,
            d => d.RuleId == Generator.RuleIdentifiers.RateLimitRetryAfterUnsupportedByAlgorithm);
    }

    // ========== RL008: window-seconds ignored by the concurrency limiter ==========
    [Fact]
    public void Validate_WindowSecondsOnConcurrency_ReportsRL008()
    {
        // Arrange - ConcurrencyLimiterOptions has no time component, so the window is dropped.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: exports
                            x-ratelimit-algorithm: concurrency
                            x-ratelimit-permit-limit: 5
                            x-ratelimit-window-seconds: 60
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
        var rl008 = diagnostics.FirstOrDefault(d =>
            d.RuleId == Generator.RuleIdentifiers.RateLimitWindowIgnoredForConcurrency);
        Assert.NotNull(rl008);
        Assert.Equal(Generator.Models.DiagnosticSeverity.Info, rl008.Severity);
        Assert.Contains("x-ratelimit-window-seconds", rl008.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WindowSecondsOnFixedWindow_NoRL008()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: orders
                            x-ratelimit-algorithm: fixed
                            x-ratelimit-window-seconds: 60
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
            d.RuleId == Generator.RuleIdentifiers.RateLimitWindowIgnoredForConcurrency));
    }

    // ========== Clean spec: none of RL004-RL008 fire ==========
    [Fact]
    public void Validate_WellFormedRateLimitSpec_ReportsNoRateLimitGuards()
    {
        // Arrange
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            x-ratelimit-policy: global
                            x-ratelimit-permit-limit: 1000
                            x-ratelimit-window-seconds: 60
                            x-ratelimit-algorithm: fixed
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
                                post:
                                  operationId: createOrder
                                  x-ratelimit-enabled: false
                                  responses:
                                    '201':
                                      description: Created
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
        var ruleIds = new[]
        {
            Generator.RuleIdentifiers.RateLimitPolicyNameCollision,
            Generator.RuleIdentifiers.RateLimitValueOutOfRange,
            Generator.RuleIdentifiers.RateLimitEnabledIgnoredOutsideOperation,
            Generator.RuleIdentifiers.RateLimitRetryAfterUnsupportedByAlgorithm,
            Generator.RuleIdentifiers.RateLimitWindowIgnoredForConcurrency,
        };

        Assert.DoesNotContain(diagnostics, d => ruleIds.Contains(d.RuleId, StringComparer.Ordinal));
    }

    // ========== Helper Methods ==========
    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, TestFilePath, out var document)
            ? document
            : null;
}