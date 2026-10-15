namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OutputCacheDependencyInjectionExtractorTests
{
    [Fact]
    public void Extract_WithOutputCachePolicies_RegistersEachPolicyInOrdinalOrderWithItsSettings()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /products:
                                x-cache-type: output
                                x-cache-policy: products
                                x-cache-expiration-seconds: 600
                                x-cache-vary-by-query:
                                  - page
                                  - size
                                x-cache-vary-by-header:
                                  - Accept-Language
                                x-cache-tags:
                                  - catalog
                                get:
                                  operationId: listProducts
                                  responses:
                                    '200':
                                      description: OK
                              /orders/{orderId}:
                                x-cache-policy: order-detail
                                x-cache-expiration-seconds: 30
                                x-cache-vary-by-route:
                                  - orderId
                                parameters:
                                  - name: orderId
                                    in: path
                                    required: true
                                    schema:
                                      type: string
                                get:
                                  operationId: getOrder
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var result = OutputCacheDependencyInjectionExtractor.Extract(document, "TestApi");

        Assert.NotNull(result);
        Assert.Contains("namespace TestApi.Generated.Caching;", result, StringComparison.Ordinal);
        Assert.Contains("public static class OutputCachingServiceCollectionExtensions", result, StringComparison.Ordinal);
        Assert.Contains("public static IServiceCollection AddApiOutputCaching(this IServiceCollection services)", result, StringComparison.Ordinal);
        Assert.Contains("services.AddOutputCache(options =>", result, StringComparison.Ordinal);

        Assert.Contains("options.AddPolicy(OutputCachePolicies.Products, policy =>", result, StringComparison.Ordinal);
        Assert.Contains("policy.Expire(TimeSpan.FromSeconds(600));", result, StringComparison.Ordinal);
        Assert.Contains("policy.SetVaryByQuery(\"page\", \"size\");", result, StringComparison.Ordinal);
        Assert.Contains("policy.SetVaryByHeader(\"Accept-Language\");", result, StringComparison.Ordinal);
        Assert.Contains("policy.Tag(OutputCacheTags.Catalog);", result, StringComparison.Ordinal);

        Assert.Contains("options.AddPolicy(OutputCachePolicies.OrderDetail, policy =>", result, StringComparison.Ordinal);
        Assert.Contains("policy.Expire(TimeSpan.FromSeconds(30));", result, StringComparison.Ordinal);
        Assert.Contains("policy.SetVaryByRouteValue(\"orderId\");", result, StringComparison.Ordinal);

        // Policies are registered by ordinal policy name, so order-detail comes before products.
        var orderDetailIndex = result.IndexOf("OutputCachePolicies.OrderDetail", StringComparison.Ordinal);
        var productsIndex = result.IndexOf("OutputCachePolicies.Products", StringComparison.Ordinal);
        Assert.True(orderDetailIndex < productsIndex, "order-detail must be registered before products");

        Assert.Contains("using System;", result, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.Extensions.DependencyInjection;", result, StringComparison.Ordinal);
        Assert.Contains("return services;", result, StringComparison.Ordinal);
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
                                  x-cache-policy: legacy
                                  x-cache-expiration-seconds: 10
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        Assert.Null(OutputCacheDependencyInjectionExtractor.Extract(document, "TestApi", includeDeprecated: false));

        var withDeprecated = OutputCacheDependencyInjectionExtractor.Extract(document, "TestApi", includeDeprecated: true);
        Assert.NotNull(withDeprecated);
        Assert.Contains("options.AddPolicy(OutputCachePolicies.Legacy, policy =>", withDeprecated, StringComparison.Ordinal);
        Assert.Contains("policy.Expire(TimeSpan.FromSeconds(10));", withDeprecated, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_OnlyHybridCachePolicies_ReturnsNull()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /users:
                                x-cache-type: hybrid
                                x-cache-policy: users
                                get:
                                  operationId: listUsers
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        Assert.Null(OutputCacheDependencyInjectionExtractor.Extract(document, "TestApi"));
    }
}