namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class OpenApiRetryExtensionsTests
{
    // ========== HasRetryConfiguration Tests ==========
    [Fact]
    public void HasRetryConfiguration_NoExtensions_ReturnsFalse()
    {
        var doc = new OpenApiDocument();

        Assert.False(doc.HasRetryConfiguration());
    }

    [Fact]
    public void HasRetryConfiguration_WithDocumentLevelPolicy_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithDocumentRetryPolicy);

        Assert.NotNull(doc);
        Assert.True(doc.HasRetryConfiguration());
    }

    [Fact]
    public void HasRetryConfiguration_WithOperationLevelPolicy_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithOperationRetryPolicy);

        Assert.NotNull(doc);
        Assert.True(doc.HasRetryConfiguration());
    }

    // ========== ExtractRetryConfiguration Tests ==========
    [Fact]
    public void ExtractRetryConfiguration_NoRetry_ReturnsNull()
    {
        var doc = ParseYaml(YamlWithNoRetry);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRetryConfiguration(
            pathItem,
            doc);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractRetryConfiguration_WithPolicy_ReturnsConfig()
    {
        var doc = ParseYaml(YamlWithOperationRetryPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRetryConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.True(result.Enabled);
        Assert.Equal("PetsRetry", result.Policy);
    }

    [Fact]
    public void ExtractRetryConfiguration_InheritsFromDocument()
    {
        var doc = ParseYaml(YamlWithDocumentRetryPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRetryConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal("GlobalRetry", result.Policy);
    }

    [Fact]
    public void ExtractRetryConfiguration_DefaultValues()
    {
        var doc = ParseYaml(YamlWithOperationRetryPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRetryConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal(3, result.MaxAttempts);
        Assert.Equal(1.0, result.DelaySeconds, precision: 5);
        Assert.True(result.UseJitter);
        Assert.True(result.Handle429);
        Assert.False(result.CircuitBreakerEnabled);
    }

    [Fact]
    public void ExtractRetryConfiguration_ReadsConfiguredIntAndDoubleValues()
    {
        // Arrange - x-retry-max-attempts is int-typed, x-retry-delay-seconds is
        // double-typed; both are boxed as decimal by the YAML reader, exercising the
        // same ExtractIntValue/ExtractDoubleValue code paths as RateLimit's Finding #0.
        var doc = ParseYaml(YamlWithFullOperationRetryConfig);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRetryConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal(5, result.MaxAttempts);
        Assert.Equal(2.5, result.DelaySeconds, precision: 5);
    }

    // ========== Extension Value Extraction Tests ==========
    [Fact]
    public void ExtractRetryPolicy_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractRetryPolicy());
    }

    [Fact]
    public void ExtractRetryEnabled_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractRetryEnabled());
    }

    [Fact]
    public void ExtractMaxRetryAttempts_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractMaxRetryAttempts());
    }

    [Fact]
    public void ExtractRetryDelaySeconds_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractRetryDelaySeconds());
    }

    [Fact]
    public void ExtractRetryCircuitBreaker_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractRetryCircuitBreaker());
    }

    // ========== ResolveOperationRetry Tests ==========
    [Theory]
    [InlineData("/items", "GET", "GET", OperationRetryMode.Default, null, null)]
    [InlineData("/items", "GET", "QUERY", OperationRetryMode.Default, null, null)]
    [InlineData("/items", "PUT", "PUT", OperationRetryMode.Default, null, null)]
    [InlineData("/items", "DELETE", "DELETE", OperationRetryMode.Default, null, null)]
    [InlineData("/items", "POST", "POST", OperationRetryMode.NoRetry, null, null)]
    [InlineData("/items", "PATCH", "PATCH", OperationRetryMode.NoRetry, null, null)]
    [InlineData("/disabled", "GET", "GET", OperationRetryMode.NoRetry, null, null)]
    [InlineData("/quiet", "GET", "GET", OperationRetryMode.NoRetry, null, 600d)]
    [InlineData("/optin", "POST", "POST", OperationRetryMode.Retry, null, null)]
    [InlineData("/own", "POST", "POST", OperationRetryMode.Retry, "own", null)]
    [InlineData("/wide", "GET", "GET", OperationRetryMode.Retry, "wide", null)]
    [InlineData("/wide", "POST", "POST", OperationRetryMode.NoRetry, "wide", null)]
    [InlineData("/wide-off", "GET", "GET", OperationRetryMode.NoRetry, "wide-off", null)]
    public void ResolveOperationRetry_FollowsTheSpecificationAndTheMethodRule(
        string path,
        string operationMethod,
        string requestMethod,
        OperationRetryMode expectedMode,
        string? expectedPolicy,
        double? expectedTimeout)
    {
        var doc = ParseYaml(YamlWithRetryResolution);
        Assert.NotNull(doc);

        var pathItem = (OpenApiPathItem)doc.Paths[path];
        var operation = pathItem.Operations!.Single(o => string.Equals(o.Key.ToString(), operationMethod, StringComparison.OrdinalIgnoreCase)).Value;

        var result = operation.ResolveOperationRetry(pathItem, doc, requestMethod);

        Assert.Equal(expectedMode, result.Mode);
        Assert.Equal(expectedPolicy, result.PolicyName);
        Assert.Equal(expectedTimeout, result.TimeoutSeconds);
    }

    [Theory]
    [InlineData("POST", true)]
    [InlineData("post", true)]
    [InlineData("PATCH", true)]
    [InlineData("GET", false)]
    [InlineData("PUT", false)]
    [InlineData("DELETE", false)]
    [InlineData("QUERY", false)]
    [InlineData("HEAD", false)]
    public void IsRetryUnsafeHttpMethod_OnlyPostAndPatch(
        string httpMethod,
        bool expected)
        => Assert.Equal(expected, OpenApiRetryExtensions.IsRetryUnsafeHttpMethod(httpMethod));

    [Fact]
    public void IsClientDefault_NoRetryForPostWithoutPolicy_NeedsNothingEmitted()
    {
        var noRetry = new OperationRetry(OperationRetryMode.NoRetry);

        Assert.True(noRetry.IsClientDefault("POST"));
        Assert.False(noRetry.IsClientDefault("GET"));
        Assert.False(new OperationRetry(OperationRetryMode.NoRetry, TimeoutSeconds: 5).IsClientDefault("POST"));
        Assert.True(OperationRetry.Default.IsClientDefault("GET"));
    }

    // ========== CollectNamedRetryPolicies Tests ==========
    [Fact]
    public void CollectNamedRetryPolicies_DocumentFirst_IncludesPoliciesOfDisabledOperations()
    {
        var doc = ParseYaml(YamlWithRetryResolution);
        Assert.NotNull(doc);

        var policies = doc.CollectNamedRetryPolicies();

        Assert.Equal(["standard", "own", "wide", "wide-off"], policies.Select(p => p.Policy), StringComparer.Ordinal);
        Assert.Equal(4, policies.Single(p => p.Policy == "own").MaxAttempts);
        Assert.Equal(1.5, policies.Single(p => p.Policy == "own").DelaySeconds);
        Assert.Equal(2, policies.Single(p => p.Policy == "standard").MaxAttempts);
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

    private const string YamlWithRetryResolution = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-retry-policy: standard
        x-retry-max-attempts: 2
        x-retry-delay-seconds: 1.5
        paths:
          /items:
            get:
              responses:
                '204':
                  description: No Content
            put:
              responses:
                '204':
                  description: No Content
            delete:
              responses:
                '204':
                  description: No Content
            post:
              responses:
                '204':
                  description: No Content
            patch:
              responses:
                '204':
                  description: No Content
          /disabled:
            x-retry-enabled: false
            get:
              responses:
                '204':
                  description: No Content
          /quiet:
            get:
              x-retry-enabled: false
              x-retry-timeout-seconds: 600
              responses:
                '204':
                  description: No Content
          /optin:
            post:
              x-retry-enabled: true
              responses:
                '204':
                  description: No Content
          /own:
            post:
              x-retry-policy: own
              x-retry-max-attempts: 4
              responses:
                '204':
                  description: No Content
          /wide:
            x-retry-policy: wide
            get:
              responses:
                '204':
                  description: No Content
            post:
              responses:
                '204':
                  description: No Content
          /wide-off:
            x-retry-policy: wide-off
            get:
              x-retry-enabled: false
              x-retry-policy: wide-off
              responses:
                '204':
                  description: No Content
        """;

    private const string YamlWithNoRetry = """
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

    private const string YamlWithDocumentRetryPolicy = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-retry-policy: GlobalRetry
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithOperationRetryPolicy = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              x-retry-policy: PetsRetry
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithFullOperationRetryConfig = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              x-retry-policy: PetsRetry
              x-retry-max-attempts: 5
              x-retry-delay-seconds: 2.5
              responses:
                '200':
                  description: OK
        """;
}