namespace Atc.Rest.Api.Generator.Tests.Extensions;

public class OpenApiCacheExtensionsTests
{
    // ========== HasCaching Tests ==========
    [Fact]
    public void HasCaching_NoExtensions_ReturnsFalse()
    {
        var doc = new OpenApiDocument();

        Assert.False(doc.HasCaching());
    }

    [Fact]
    public void HasCaching_WithDocumentLevelPolicy_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithDocumentCachePolicy);

        Assert.NotNull(doc);
        Assert.True(doc!.HasCaching());
    }

    [Fact]
    public void HasCaching_WithOperationLevelPolicy_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithOperationCachePolicy);

        Assert.NotNull(doc);
        Assert.True(doc!.HasCaching());
    }

    // ========== ExtractCacheConfiguration Tests ==========
    [Fact]
    public void ExtractCacheConfiguration_NoCaching_ReturnsNull()
    {
        var doc = ParseYaml(YamlWithNoCaching);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractCacheConfiguration(
            pathItem,
            doc);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractCacheConfiguration_WithPolicy_ReturnsConfig()
    {
        var doc = ParseYaml(YamlWithOperationCachePolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractCacheConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.True(result!.Enabled);
        Assert.Equal("GetPets", result.Policy);
    }

    [Fact]
    public void ExtractCacheConfiguration_InheritsFromDocument()
    {
        var doc = ParseYaml(YamlWithDocumentCachePolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc!);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractCacheConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal("GlobalCache", result!.Policy);
    }

    // ========== HasOutputCaching / HasHybridCaching Tests ==========
    [Fact]
    public void HasOutputCaching_DefaultType_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithDocumentCachePolicy);
        Assert.NotNull(doc);

        Assert.True(doc!.HasOutputCaching());
    }

    [Fact]
    public void HasHybridCaching_NoHybrid_ReturnsFalse()
    {
        var doc = ParseYaml(YamlWithDocumentCachePolicy);
        Assert.NotNull(doc);

        Assert.False(doc!.HasHybridCaching());
    }

    // ========== Extension Value Extraction Tests ==========
    [Fact]
    public void ExtractCacheType_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractCacheType());
    }

    [Fact]
    public void ExtractCachePolicy_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractCachePolicy());
    }

    [Fact]
    public void ExtractCacheEnabled_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractCacheEnabled());
    }

    [Fact]
    public void ExtractCacheExpirationSeconds_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractCacheExpirationSeconds());
    }

    [Fact]
    public void ExtractCacheTags_NullExtensions_ReturnsNull()
    {
        IDictionary<string, IOpenApiExtension>? extensions = null;

        Assert.Null(extensions.ExtractCacheTags());
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

    private const string YamlWithNoCaching = """
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

    private const string YamlWithDocumentCachePolicy = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        x-cache-policy: GlobalCache
        paths:
          /pets:
            get:
              operationId: getPets
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithOperationCachePolicy = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              x-cache-policy: GetPets
              responses:
                '200':
                  description: OK
        """;
}