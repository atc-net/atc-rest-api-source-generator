namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for <see cref="RateLimitDependencyInjectionExtractor"/> covering the configure callback
/// parameter, the default Retry-After emission via <c>options.OnRejected</c>, and partitioned
/// limiters (ip/user) across all four rate-limit algorithms.
/// </summary>
public class RateLimitDependencyInjectionExtractorTests
{
    // ========== Configure callback (Option A) ==========
    [Fact]
    public void Extract_WithPolicies_GeneratesConfigureCallbackParameter()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlSinglePolicy);

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.NotNull(result);
        Assert.Contains("AddApiRateLimiting(", result, StringComparison.Ordinal);
        Assert.Contains("this IServiceCollection services,", result, StringComparison.Ordinal);
        Assert.Contains("Action<RateLimiterOptions>? configure = null)", result, StringComparison.Ordinal);
        Assert.Contains("/// <param name=\"configure\">", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithPolicies_InvokesConfigureAsLastStatementInOptionsLambda()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlSinglePolicy);

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.NotNull(result);
        Assert.Contains("configure?.Invoke(options);", result, StringComparison.Ordinal);

        var lastOptionsAddIndex = result.LastIndexOf("options.Add", StringComparison.Ordinal);
        var configureInvokeIndex = result.IndexOf("configure?.Invoke(options);", StringComparison.Ordinal);
        var closingLambdaIndex = result.LastIndexOf("});", StringComparison.Ordinal);

        Assert.True(lastOptionsAddIndex >= 0, "Expected at least one options.Add* call");
        Assert.True(lastOptionsAddIndex < configureInvokeIndex, "configure?.Invoke should come after the last options.Add* call");
        Assert.True(configureInvokeIndex < closingLambdaIndex, "configure?.Invoke should come before the closing '});' of the AddRateLimiter lambda");
    }

    // ========== OnRejected / Retry-After (Option B) ==========
    [Fact]
    public void Extract_ByDefault_EmitsOnRejectedWithRetryAfter()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlSinglePolicy);

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.NotNull(result);
        Assert.Contains("options.OnRejected", result, StringComparison.Ordinal);
        Assert.Contains("MetadataName.RetryAfter", result, StringComparison.Ordinal);
        Assert.Contains("context.HttpContext.Response.Headers.RetryAfter", result, StringComparison.Ordinal);
        Assert.Contains("NumberFormatInfo.InvariantInfo", result, StringComparison.Ordinal);
        Assert.Contains("ValueTask.CompletedTask", result, StringComparison.Ordinal);

        var onRejectedIndex = result.IndexOf("options.OnRejected", StringComparison.Ordinal);
        var configureInvokeIndex = result.IndexOf("configure?.Invoke(options);", StringComparison.Ordinal);
        Assert.True(onRejectedIndex < configureInvokeIndex, "options.OnRejected should be assigned before configure?.Invoke is called");
    }

    [Fact]
    public void Extract_WithEmitRetryAfterFalse_OmitsOnRejected()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlEmitRetryAfterFalse);

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.NotNull(result);
        Assert.DoesNotContain("OnRejected", result, StringComparison.Ordinal);

        // Both namespaces are gated on the same EmitRetryAfter flag - assert the whole gate.
        Assert.DoesNotContain("using System.Globalization;", result, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Threading.Tasks;", result, StringComparison.Ordinal);

        // Option A (configure callback) is unconditional and must still be emitted.
        Assert.Contains("Action<RateLimiterOptions>? configure = null)", result, StringComparison.Ordinal);
        Assert.Contains("configure?.Invoke(options);", result, StringComparison.Ordinal);
    }

    // ========== Partitioning (Option C) ==========
    [Fact]
    public void Extract_WithGlobalPartition_EmitsBuiltInLimiterUnchanged()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlSinglePolicy);

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.NotNull(result);
        Assert.Contains("options.AddFixedWindowLimiter(RateLimitPolicies.", result, StringComparison.Ordinal);
        Assert.DoesNotContain("options.AddPolicy(", result, StringComparison.Ordinal);
        Assert.DoesNotContain("RateLimitPartition.", result, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Security.Claims;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithIpPartition_EmitsPartitionedLimiterKeyedByRemoteIp()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlPartitionIp);

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.NotNull(result);
        Assert.Contains("options.AddPolicy(RateLimitPolicies.", result, StringComparison.Ordinal);
        Assert.Contains("RateLimitPartition.GetFixedWindowLimiter(", result, StringComparison.Ordinal);
        Assert.Contains("new FixedWindowRateLimiterOptions", result, StringComparison.Ordinal);
        Assert.Contains("httpContext.Connection.RemoteIpAddress?.ToString() ?? \"unknown\"", result, StringComparison.Ordinal);
        Assert.DoesNotContain("options.AddFixedWindowLimiter(", result, StringComparison.Ordinal);
        Assert.DoesNotContain("ClaimTypes", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithUserPartition_UsesSubClaimByDefault()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlPartitionUserDefaultClaim);

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.NotNull(result);
        Assert.Contains("httpContext.User.FindFirst(\"sub\")?.Value", result, StringComparison.Ordinal);
        Assert.Contains("?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value", result, StringComparison.Ordinal);
        Assert.Contains("?? httpContext.Connection.RemoteIpAddress?.ToString()", result, StringComparison.Ordinal);
        Assert.Contains("?? \"anonymous\"", result, StringComparison.Ordinal);
        Assert.Contains("using System.Security.Claims;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithUserPartitionAndCustomClaim_UsesConfiguredClaim()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlPartitionUserCustomClaim);

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.NotNull(result);
        Assert.Contains("FindFirst(\"oid\")", result, StringComparison.Ordinal);
        Assert.DoesNotContain("FindFirst(\"sub\")", result, StringComparison.Ordinal);
        Assert.Contains("ClaimTypes.NameIdentifier", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fixed", "RateLimitPartition.GetFixedWindowLimiter(", "new FixedWindowRateLimiterOptions")]
    [InlineData("sliding", "RateLimitPartition.GetSlidingWindowLimiter(", "new SlidingWindowRateLimiterOptions")]
    [InlineData("token-bucket", "RateLimitPartition.GetTokenBucketLimiter(", "new TokenBucketRateLimiterOptions")]
    [InlineData("concurrency", "RateLimitPartition.GetConcurrencyLimiter(", "new ConcurrencyLimiterOptions")]
    public void Extract_WithUserPartition_AcrossAllAlgorithms_EmitsMatchingPartitionFactoryAndOptionsType(
        string algorithm,
        string expectedPartitionFactory,
        string expectedOptionsType)
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlPartitionUserWithAlgorithm(algorithm));

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.NotNull(result);
        Assert.Contains(expectedPartitionFactory, result, StringComparison.Ordinal);
        Assert.Contains(expectedOptionsType, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithMixedPartitions_EmitsBothFormsAndAllRequiredUsings()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlMixedPartitions);

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.NotNull(result);
        Assert.Contains("options.AddFixedWindowLimiter(", result, StringComparison.Ordinal);
        Assert.Contains("options.AddPolicy(", result, StringComparison.Ordinal);
        Assert.Contains("using System.Globalization;", result, StringComparison.Ordinal);
        Assert.Contains("using System.Security.Claims;", result, StringComparison.Ordinal);
        Assert.Contains("using System.Threading.Tasks;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithPolicyRedeclaredAtPathLevel_KeepsDocumentLevelConfiguration()
    {
        // A policy name declared at BOTH document and path level is resolved first-wins, and the
        // document level is collected before any path, so the document's configuration wins. This
        // is what keeps the emitted output independent of Paths iteration order - see the
        // /webhooks path in test/Scenarios/RateLimit/RateLimit.yaml, which re-declares "global".
        var doc = OpenApiDocumentHelper.ParseYaml(YamlPolicyRedeclaredAtPathLevel);

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.NotNull(result);
        Assert.Contains("options.AddFixedWindowLimiter(RateLimitPolicies.Global, opt =>", result, StringComparison.Ordinal);
        Assert.DoesNotContain("options.AddPolicy(", result, StringComparison.Ordinal);
        Assert.DoesNotContain("RateLimitPartition.", result, StringComparison.Ordinal);
    }

    // ========== Guard clauses ==========
    [Fact]
    public void Extract_WithNoPolicies_ReturnsNull()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(YamlNoPolicies);

        var result = RateLimitDependencyInjectionExtractor.Extract(doc, "TestProject");

        Assert.Null(result);
    }

    [Fact]
    public void Extract_WithNullDocument_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(
            () => RateLimitDependencyInjectionExtractor.Extract(null!, "TestProject"));

    // ========== YAML fixtures ==========
    private const string YamlSinglePolicy = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-ratelimit-policy: global
        x-ratelimit-permit-limit: 100
        x-ratelimit-window-seconds: 60
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlEmitRetryAfterFalse = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-ratelimit-policy: global
        x-ratelimit-permit-limit: 100
        x-ratelimit-window-seconds: 60
        x-ratelimit-emit-retry-after: false
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlPolicyRedeclaredAtPathLevel = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-ratelimit-policy: global
        x-ratelimit-permit-limit: 100
        x-ratelimit-window-seconds: 60
        paths:
          /pets:
            x-ratelimit-policy: global
            x-ratelimit-partition: user
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlPartitionIp = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-ratelimit-policy: global
        x-ratelimit-permit-limit: 100
        x-ratelimit-window-seconds: 60
        x-ratelimit-partition: ip
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlPartitionUserDefaultClaim = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-ratelimit-policy: global
        x-ratelimit-permit-limit: 100
        x-ratelimit-window-seconds: 60
        x-ratelimit-partition: user
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlPartitionUserCustomClaim = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-ratelimit-policy: global
        x-ratelimit-permit-limit: 100
        x-ratelimit-window-seconds: 60
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

    private const string YamlMixedPartitions = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-ratelimit-policy: global
        x-ratelimit-permit-limit: 1000
        x-ratelimit-window-seconds: 60
        paths:
          /health:
            get:
              operationId: getHealth
              responses:
                '200':
                  description: OK
          /reports:
            x-ratelimit-policy: reports-user
            x-ratelimit-partition: user
            x-ratelimit-permit-limit: 50
            x-ratelimit-window-seconds: 60
            get:
              operationId: getReports
              responses:
                '200':
                  description: OK
        """;

    private const string YamlNoPolicies = """
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

    private static string YamlPartitionUserWithAlgorithm(string algorithm)
        => $"""
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-ratelimit-policy: global
        x-ratelimit-permit-limit: 100
        x-ratelimit-window-seconds: 60
        x-ratelimit-partition: user
        x-ratelimit-algorithm: {algorithm}
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;
}