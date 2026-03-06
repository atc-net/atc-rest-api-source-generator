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
        Assert.True(doc!.HasRateLimiting());
    }

    [Fact]
    public void HasRateLimiting_WithOperationLevelPolicy_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithOperationRateLimitPolicy);

        Assert.NotNull(doc);
        Assert.True(doc!.HasRateLimiting());
    }

    // ========== ExtractRateLimitConfiguration Tests ==========
    [Fact]
    public void ExtractRateLimitConfiguration_NoRateLimit_ReturnsNull()
    {
        var doc = ParseYaml(YamlWithNoRateLimit);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
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

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.True(result!.Enabled);
        Assert.Equal("PetsPolicy", result.Policy);
    }

    [Fact]
    public void ExtractRateLimitConfiguration_InheritsFromDocument()
    {
        var doc = ParseYaml(YamlWithDocumentRateLimitPolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractRateLimitConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal("GlobalPolicy", result!.Policy);
    }

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
}