namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class OpenApiRateLimitExtensionsTests
{
    // ========== HasRateLimiting Tests ==========
    [Fact]
    public void HasRateLimiting_NoExtensions_ReturnsFalse()
    {
        var doc = new OpenApiDocument();

        Assert.False(doc.HasRateLimiting());
    }

    [Fact]
    public void HasRateLimiting_WithDocumentLevelPolicy_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithDocumentRateLimitPolicy);

        Assert.NotNull(doc);
        Assert.True(doc.HasRateLimiting());
    }

    [Fact]
    public void HasRateLimiting_WithOperationLevelPolicy_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithOperationRateLimitPolicy);

        Assert.NotNull(doc);
        Assert.True(doc.HasRateLimiting());
    }

    // ========== ExtractRateLimitConfiguration Tests ==========
    [Fact]
    public void ExtractRateLimitConfiguration_NoRateLimit_ReturnsNull()
    {
        var doc = ParseYaml(YamlWithNoRateLimit);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_WithPolicy_ReturnsConfig()
    {
        var doc = ParseYaml(YamlWithOperationRateLimitPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.True(result.Enabled);
        Assert.Equal("PetsPolicy", result.Policy);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_InheritsFromDocument()
    {
        var doc = ParseYaml(YamlWithDocumentRateLimitPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal("GlobalPolicy", result.Policy);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_DefaultValues_WhenNotSpecified()
    {
        var doc = ParseYaml(YamlWithOperationRateLimitPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal(100, result.PermitLimit);
        Assert.Equal(60, result.WindowSeconds);
        Assert.Equal(0, result.QueueLimit);
        Assert.Equal(RateLimitAlgorithm.Fixed, result.Algorithm);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_ReadsAllConfiguredValues()
    {
        var doc = ParseYaml(YamlWithFullOperationRateLimitConfig);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal("LogsRead", result.Policy);
        Assert.Equal(30, result.PermitLimit);
        Assert.Equal(60, result.WindowSeconds);
        Assert.Equal(0, result.QueueLimit);
        Assert.Equal(RateLimitAlgorithm.Sliding, result.Algorithm);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_OperationOverridesPathValues()
    {
        var doc = ParseYaml(YamlWithPathAndOperationRateLimitOverrides);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal("PetsStrict", result.Policy);
        Assert.Equal(10, result.PermitLimit);
        Assert.Equal(30, result.WindowSeconds);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_PathValues_InheritWhenOperationSilent()
    {
        // The path item configures numeric values; the operation only re-declares the
        // policy name (as OAS requires a policy to apply rate limiting at all) without
        // overriding permit-limit/window-seconds, so those must inherit from the path.
        var doc = ParseYaml(YamlWithPathLevelNumericValues);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal("PetsPath", result.Policy);
        Assert.Equal(50, result.PermitLimit);
        Assert.Equal(120, result.WindowSeconds);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_EnabledFalse_ReturnsDisabledConfigWithoutPolicy()
    {
        var doc = ParseYaml(YamlWithRateLimitDisabledOnOperation);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.False(result.Enabled);
        Assert.Null(result.Policy);
    }

    [Theory]
    [InlineData("sliding", RateLimitAlgorithm.Sliding)]
    [InlineData("sliding-window", RateLimitAlgorithm.Sliding)]
    [InlineData("token-bucket", RateLimitAlgorithm.TokenBucket)]
    [InlineData("tokenbucket", RateLimitAlgorithm.TokenBucket)]
    [InlineData("concurrency", RateLimitAlgorithm.Concurrency)]
    [InlineData("fixed", RateLimitAlgorithm.Fixed)]
    [InlineData("not-a-real-algorithm", RateLimitAlgorithm.Fixed)]
    public void ExtractRateLimitConfiguration_ParsesAlgorithm(
        string algorithmValue,
        RateLimitAlgorithm expected)
    {
        var doc = ParseYaml(YamlWithAlgorithm(algorithmValue));
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Algorithm);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_NoAlgorithmSpecified_DefaultsToFixed()
    {
        var doc = ParseYaml(YamlWithOperationRateLimitPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal(RateLimitAlgorithm.Fixed, result.Algorithm);
    }

    // ========== EmitRetryAfter Tests ==========
    [Fact]
    public void ExtractRateLimitConfiguration_EmitRetryAfter_DefaultsToTrue_WhenAbsent()
    {
        var doc = ParseYaml(YamlWithOperationRateLimitPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.True(result.EmitRetryAfter);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_EmitRetryAfterFalseAtDocumentLevel_IsHonoured()
    {
        var doc = ParseYaml(YamlWithDocumentLevelEmitRetryAfterFalse);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.False(result.EmitRetryAfter);
    }

    // ========== Partition Tests ==========
    [Fact]
    public void ExtractRateLimitConfiguration_Partition_DefaultsToGlobal_WhenAbsent()
    {
        var doc = ParseYaml(YamlWithOperationRateLimitPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal(RateLimitPartitionStrategy.Global, result.Partition);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_PartitionIpAtPathLevel_ResolvesToIp()
    {
        var doc = ParseYaml(YamlWithPathLevelPartitionIp);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal(RateLimitPartitionStrategy.Ip, result.Partition);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_PartitionUserWithClaimAtOperationLevel_OverridesPathLevel()
    {
        var doc = ParseYaml(YamlWithOperationPartitionUserOverridingPathIp);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal(RateLimitPartitionStrategy.User, result.Partition);
        Assert.Equal("oid", result.PartitionClaim);
    }

    // ========== ParsePartitionStrategy Tests ==========
    [Theory]
    [InlineData("global", RateLimitPartitionStrategy.Global)]
    [InlineData("ip", RateLimitPartitionStrategy.Ip)]
    [InlineData("user", RateLimitPartitionStrategy.User)]
    [InlineData("USER", RateLimitPartitionStrategy.User)]
    [InlineData("", RateLimitPartitionStrategy.Global)]
    [InlineData(null, RateLimitPartitionStrategy.Global)]
    [InlineData("bogus", RateLimitPartitionStrategy.Global)]
    public void ParsePartitionStrategy_ReturnsExpectedResult(
        string? partitionValue,
        RateLimitPartitionStrategy expected)
        => Assert.Equal(expected, OpenApiRateLimitExtensions.ParsePartitionStrategy(partitionValue));

    // ========== Extension Value Extraction Tests ==========
    [Fact]
    public void ExtractRateLimitPolicy_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractRateLimitPolicy());
    }

    [Fact]
    public void ExtractRateLimitEnabled_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractRateLimitEnabled());
    }

    [Fact]
    public void ExtractPermitLimit_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractPermitLimit());
    }

    [Fact]
    public void ExtractWindowSeconds_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractWindowSeconds());
    }

    [Fact]
    public void ExtractQueueLimit_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractQueueLimit());
    }

    [Fact]
    public void ExtractRateLimitAlgorithm_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractRateLimitAlgorithm());
    }

    // ========== Helper Methods ==========
    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(
            yaml,
            "test.yaml",
            out var document)
            ? document
            : null;

    private static OpenApiPathItem GetFirstPathItem(OpenApiDocument doc)
        => (OpenApiPathItem)doc.Paths.First().Value;

    private static OpenApiOperation GetFirstOperation(OpenApiPathItem pathItem)
        => pathItem.Operations.First().Value;

    private const string YamlWithNoRateLimit = """
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

    private const string YamlWithDocumentRateLimitPolicy = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-ratelimit-policy: GlobalPolicy
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithOperationRateLimitPolicy = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              x-ratelimit-policy: PetsPolicy
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithFullOperationRateLimitConfig = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /logs:
            get:
              operationId: getLogs
              x-ratelimit-policy: LogsRead
              x-ratelimit-algorithm: sliding
              x-ratelimit-permit-limit: 30
              x-ratelimit-window-seconds: 60
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithPathAndOperationRateLimitOverrides = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            x-ratelimit-policy: PetsPath
            x-ratelimit-permit-limit: 50
            x-ratelimit-window-seconds: 120
            get:
              operationId: getPets
              x-ratelimit-policy: PetsStrict
              x-ratelimit-permit-limit: 10
              x-ratelimit-window-seconds: 30
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithPathLevelNumericValues = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            x-ratelimit-policy: PetsPath
            x-ratelimit-permit-limit: 50
            x-ratelimit-window-seconds: 120
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithRateLimitDisabledOnOperation = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-ratelimit-policy: GlobalPolicy
        paths:
          /pets:
            get:
              operationId: getPets
              x-ratelimit-enabled: false
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithDocumentLevelEmitRetryAfterFalse = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-ratelimit-policy: GlobalPolicy
        x-ratelimit-emit-retry-after: false
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithPathLevelPartitionIp = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            x-ratelimit-policy: PetsPath
            x-ratelimit-partition: ip
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithOperationPartitionUserOverridingPathIp = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            x-ratelimit-policy: PetsPath
            x-ratelimit-partition: ip
            get:
              operationId: getPets
              x-ratelimit-policy: PetsStrict
              x-ratelimit-partition: user
              x-ratelimit-partition-claim: oid
              responses:
                '200':
                  description: OK
        """;

    private static string YamlWithAlgorithm(string algorithm) => $"""
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              x-ratelimit-policy: PetsPolicy
              x-ratelimit-algorithm: {algorithm}
              responses:
                '200':
                  description: OK
        """;
}