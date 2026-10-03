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
        Assert.True(doc.HasCaching());
    }

    [Fact]
    public void HasCaching_WithOperationLevelPolicy_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithOperationCachePolicy);

        Assert.NotNull(doc);
        Assert.True(doc.HasCaching());
    }

    // ========== ExtractCacheConfiguration Tests ==========
    [Fact]
    public void ExtractCacheConfiguration_NoCaching_ReturnsNull()
    {
        var doc = ParseYaml(YamlWithNoCaching);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
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

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractCacheConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.True(result.Enabled);
        Assert.Equal("GetPets", result.Policy);
    }

    [Fact]
    public void ExtractCacheConfiguration_InheritsFromDocument()
    {
        var doc = ParseYaml(YamlWithDocumentCachePolicy);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractCacheConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal("GlobalCache", result.Policy);
    }

    [Fact]
    public void ExtractCacheConfiguration_ReadsConfiguredExpirationSeconds()
    {
        // Arrange - x-cache-expiration-seconds is boxed as decimal by the YAML reader,
        // exercising the same ExtractIntValue code path as RateLimit's Finding #0.
        var doc = ParseYaml(YamlWithExpirationSeconds);
        Assert.NotNull(doc);

        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractCacheConfiguration(
            pathItem,
            doc);

        Assert.NotNull(result);
        Assert.Equal(45, result.ExpirationSeconds);
    }

    // ========== HasOutputCaching / HasHybridCaching Tests ==========
    [Fact]
    public void HasOutputCaching_DefaultType_ReturnsTrue()
    {
        var doc = ParseYaml(YamlWithDocumentCachePolicy);
        Assert.NotNull(doc);

        Assert.True(doc.HasOutputCaching());
    }

    [Fact]
    public void HasHybridCaching_NoHybrid_ReturnsFalse()
    {
        var doc = ParseYaml(YamlWithDocumentCachePolicy);
        Assert.NotNull(doc);

        Assert.False(doc.HasHybridCaching());
    }

    // ========== ParseCacheType / ParseCacheMode / MergeTags Tests ==========
    [Theory]
    [InlineData("hybrid", CacheType.Hybrid)]
    [InlineData("HybridCache", CacheType.Hybrid)]
    [InlineData("output", CacheType.Output)]
    [InlineData("bogus", CacheType.Output)]
    [InlineData("", CacheType.Output)]
    [InlineData(null, CacheType.Output)]
    public void ParseCacheType_ReturnsExpectedResult(
        string? typeValue,
        CacheType expected)
        => Assert.Equal(expected, OpenApiCacheExtensions.ParseCacheType(typeValue));

    [Theory]
    [InlineData("in-memory", CacheMode.InMemory)]
    [InlineData("InMemory", CacheMode.InMemory)]
    [InlineData("memory", CacheMode.InMemory)]
    [InlineData("distributed", CacheMode.Distributed)]
    [InlineData("L2", CacheMode.Distributed)]
    [InlineData("hybrid", CacheMode.Hybrid)]
    [InlineData("bogus", CacheMode.Hybrid)]
    [InlineData("", CacheMode.Hybrid)]
    [InlineData(null, CacheMode.Hybrid)]
    public void ParseCacheMode_ReturnsExpectedResult(
        string? modeValue,
        CacheMode expected)
        => Assert.Equal(expected, OpenApiCacheExtensions.ParseCacheMode(modeValue));

    [Fact]
    public void MergeTags_AllLevels_AreUnionedDeduplicatedAndSorted()
    {
        var result = OpenApiCacheExtensions.MergeTags(
            ["pets", "global"],
            ["pets", "path"],
            ["op"]);

        Assert.Equal(["global", "op", "path", "pets"], result);
    }

    [Fact]
    public void MergeTags_NullLevels_AreSkipped()
    {
        Assert.Equal(["only"], OpenApiCacheExtensions.MergeTags(null, null, ["only"]));
        Assert.Empty(OpenApiCacheExtensions.MergeTags(null, null, null));
    }

    [Fact]
    public void MergeTags_IsCaseSensitive()
        => Assert.Equal(["Pets", "pets"], OpenApiCacheExtensions.MergeTags(["pets"], ["Pets"], null));

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

    [Fact]
    public void ExtractCacheVaryByQuery_OperationDeclaresQueryNames_ReturnsThem()
    {
        var operation = GetFirstOperation(GetFirstPathItem(ParseYaml(YamlWithHybridCacheValues)!));

        var result = operation.Extensions.ExtractCacheVaryByQuery();

        Assert.Equal(["page", "size"], result);
    }

    [Fact]
    public void ExtractCacheVaryByQuery_NotDeclared_ReturnsNull()
    {
        var operation = GetFirstOperation(GetFirstPathItem(ParseYaml(YamlWithOperationCachePolicy)!));

        Assert.Null(operation.Extensions.ExtractCacheVaryByQuery());
    }

    [Fact]
    public void ExtractCacheVaryByHeader_OperationDeclaresHeaderNames_ReturnsThem()
    {
        var operation = GetFirstOperation(GetFirstPathItem(ParseYaml(YamlWithHybridCacheValues)!));

        var result = operation.Extensions.ExtractCacheVaryByHeader();

        Assert.Equal(["Accept-Language"], result);
    }

    [Fact]
    public void ExtractCacheVaryByRoute_OperationDeclaresRouteValues_ReturnsThem()
    {
        var operation = GetFirstOperation(GetFirstPathItem(ParseYaml(YamlWithHybridCacheValues)!));

        var result = operation.Extensions.ExtractCacheVaryByRoute();

        Assert.Equal(["tenantId"], result);
    }

    [Fact]
    public void ExtractCacheMode_OperationDeclaresMode_ReturnsRawString()
    {
        var operation = GetFirstOperation(GetFirstPathItem(ParseYaml(YamlWithHybridCacheValues)!));

        Assert.Equal("in-memory", operation.Extensions.ExtractCacheMode());
    }

    [Fact]
    public void ExtractCacheMode_NotDeclared_ReturnsNull()
    {
        var operation = GetFirstOperation(GetFirstPathItem(ParseYaml(YamlWithOperationCachePolicy)!));

        Assert.Null(operation.Extensions.ExtractCacheMode());
    }

    [Fact]
    public void ExtractCacheSlidingExpirationSeconds_OperationDeclaresSeconds_ReturnsInt()
    {
        var operation = GetFirstOperation(GetFirstPathItem(ParseYaml(YamlWithHybridCacheValues)!));

        Assert.Equal(90, operation.Extensions.ExtractCacheSlidingExpirationSeconds());
    }

    [Fact]
    public void ExtractCacheSlidingExpirationSeconds_NotDeclared_ReturnsNull()
    {
        var operation = GetFirstOperation(GetFirstPathItem(ParseYaml(YamlWithOperationCachePolicy)!));

        Assert.Null(operation.Extensions.ExtractCacheSlidingExpirationSeconds());
    }

    [Fact]
    public void ExtractCacheKeyPrefix_OperationDeclaresPrefix_ReturnsIt()
    {
        var operation = GetFirstOperation(GetFirstPathItem(ParseYaml(YamlWithHybridCacheValues)!));

        Assert.Equal("pets:", operation.Extensions.ExtractCacheKeyPrefix());
    }

    [Fact]
    public void ExtractCacheConfiguration_HybridValues_FlowIntoConfiguration()
    {
        var doc = ParseYaml(YamlWithHybridCacheValues);
        Assert.NotNull(doc);
        var pathItem = GetFirstPathItem(doc);
        var operation = GetFirstOperation(pathItem);

        var result = operation.ExtractCacheConfiguration(pathItem, doc);

        Assert.NotNull(result);
        Assert.Equal(CacheType.Hybrid, result.Type);
        Assert.Equal(CacheMode.InMemory, result.Mode);
        Assert.Equal(90, result.SlidingExpirationSeconds);
        Assert.Equal("pets:", result.KeyPrefix);
        Assert.Equal(["page", "size"], result.VaryByQuery);
        Assert.Equal(["Accept-Language"], result.VaryByHeader);
        Assert.Equal(["tenantId"], result.VaryByRoute);
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

    private const string YamlWithExpirationSeconds = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              x-cache-policy: GetPets
              x-cache-expiration-seconds: 45
              responses:
                '200':
                  description: OK
        """;

    private const string YamlWithHybridCacheValues = """
        openapi: 3.0.0
        info:
          title: Test API
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: getPets
              x-cache-policy: GetPets
              x-cache-type: hybrid
              x-cache-mode: in-memory
              x-cache-sliding-expiration-seconds: 90
              x-cache-key-prefix: 'pets:'
              x-cache-vary-by-query:
                - page
                - size
              x-cache-vary-by-header:
                - Accept-Language
              x-cache-vary-by-route:
                - tenantId
              responses:
                '200':
                  description: OK
        """;
}