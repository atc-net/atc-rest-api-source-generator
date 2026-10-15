namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class HybridCachePoliciesExtractorTests
{
    [Fact]
    public void Extract_WithHybridCachePolicies_ProducesSortedConstantsWithConfigurationInDocComment()
    {
        // Two path-level hybrid policies; the second one is declared first to show the ordinal sort.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /users/{userId}:
                                x-cache-type: hybrid
                                x-cache-policy: user-detail
                                x-cache-expiration-seconds: 300
                                x-cache-mode: distributed
                                x-cache-sliding-expiration-seconds: 60
                                x-cache-key-prefix: user
                                x-cache-tags:
                                  - users
                                  - user-detail
                                parameters:
                                  - name: userId
                                    in: path
                                    required: true
                                    schema:
                                      type: string
                                get:
                                  operationId: getUser
                                  responses:
                                    '200':
                                      description: OK
                              /accounts:
                                x-cache-type: hybrid
                                x-cache-policy: accounts
                                x-cache-expiration-seconds: 600
                                get:
                                  operationId: listAccounts
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = HybridCachePoliciesExtractor.Extract(document, "TestApi");

        Assert.NotNull(result);
        Assert.Contains("namespace TestApi.Generated.Caching;", result, StringComparison.Ordinal);
        Assert.Contains("public static class CachePolicies", result, StringComparison.Ordinal);
        Assert.Contains("public const string Accounts = \"accounts\";", result, StringComparison.Ordinal);
        Assert.Contains("public const string UserDetail = \"user-detail\";", result, StringComparison.Ordinal);

        // The doc comment describes the resolved configuration; the default mode is Hybrid and tags are sorted.
        Assert.Contains("/// Policy: 600s expiration, mode: Hybrid", result, StringComparison.Ordinal);
        Assert.Contains("/// Policy: 300s expiration, mode: Distributed, sliding: 60s, tags: [user-detail, users], key prefix: user", result, StringComparison.Ordinal);

        // The tags have constants of their own (what HybridCache.RemoveByTagAsync takes)
        Assert.Contains("public static class CacheTags", result, StringComparison.Ordinal);
        Assert.Contains("public const string Users = \"users\";", result, StringComparison.Ordinal);

        var dependencyInjection = HybridCacheDependencyInjectionExtractor.Extract(document, "TestApi");
        Assert.NotNull(dependencyInjection);
        Assert.Contains("CachePolicies.UserDetail => [CacheTags.UserDetail, CacheTags.Users],", dependencyInjection, StringComparison.Ordinal);

        var accountsIndex = result.IndexOf("Accounts =", StringComparison.Ordinal);
        var userDetailIndex = result.IndexOf("UserDetail =", StringComparison.Ordinal);
        Assert.True(accountsIndex < userDetailIndex, "Constants must be sorted by policy name");
    }

    [Fact]
    public void Extract_OnlyOutputCachePolicies_ReturnsNull()
    {
        // x-cache-type defaults to output, which the HybridCache extractor does not own.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /products:
                                x-cache-policy: products
                                x-cache-expiration-seconds: 600
                                get:
                                  operationId: listProducts
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        Assert.Null(HybridCachePoliciesExtractor.Extract(document, "TestApi"));
    }

    [Fact]
    public void Extract_PolicyOnDeprecatedOperation_OnlyIncludedWhenRequested()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /legacy:
                                get:
                                  operationId: getLegacy
                                  deprecated: true
                                  x-cache-type: hybrid
                                  x-cache-policy: legacy
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        Assert.Null(HybridCachePoliciesExtractor.Extract(document, "TestApi", includeDeprecated: false));

        var withDeprecated = HybridCachePoliciesExtractor.Extract(document, "TestApi", includeDeprecated: true);
        Assert.NotNull(withDeprecated);
        Assert.Contains("public const string Legacy = \"legacy\";", withDeprecated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("users", "Users")]
    [InlineData("user-detail", "UserDetail")]
    [InlineData("user_detail", "UserDetail")]
    [InlineData("catalog:products by region", "CatalogProductsByRegion")]
    public void GenerateConstantName_PolicyName_IsPascalCasedPerSeparator(
        string policyName,
        string expected)
    {
        Assert.Equal(expected, HybridCachePoliciesExtractor.GenerateConstantName(policyName));
    }
}