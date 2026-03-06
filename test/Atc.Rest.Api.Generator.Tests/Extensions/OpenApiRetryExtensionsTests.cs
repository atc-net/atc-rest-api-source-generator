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
        Assert.True(doc!.HasRetryConfiguration());
    }

    [Fact]
    public void HasRetryConfiguration_WithOperationLevelPolicy_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithOperationRetryPolicy);

        Assert.NotNull(doc);
        Assert.True(doc!.HasRetryConfiguration());
    }

    // ========== ExtractRetryConfiguration Tests ==========
    [Fact]
    public void ExtractRetryConfiguration_NoRetry_ReturnsNull()
    {
        var doc = ParseYaml(YamlWithNoRetry);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
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

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRetryConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.True(result!.Enabled);
        Assert.Equal("PetsRetry", result.Policy);
    }

    [Fact]
    public void ExtractRetryConfiguration_InheritsFromDocument()
    {
        var doc = ParseYaml(YamlWithDocumentRetryPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRetryConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal("GlobalRetry", result!.Policy);
    }

    [Fact]
    public void ExtractRetryConfiguration_DefaultValues()
    {
        var doc = ParseYaml(YamlWithOperationRetryPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRetryConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal(3, result!.MaxAttempts);
        Assert.Equal(1.0, result.DelaySeconds);
        Assert.True(result.UseJitter);
        Assert.True(result.Handle429);
        Assert.False(result.CircuitBreakerEnabled);
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
}