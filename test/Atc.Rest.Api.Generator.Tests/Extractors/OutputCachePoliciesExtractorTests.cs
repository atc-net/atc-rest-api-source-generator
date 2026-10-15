namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for the constant naming of <see cref="OutputCachePoliciesExtractor"/>.
/// The Extract method is covered by <see cref="PolicyExtractorTests"/>.
/// </summary>
public class OutputCachePoliciesExtractorTests
{
    [Theory]
    [InlineData("products", "Products")]
    [InlineData("product-detail", "ProductDetail")]
    [InlineData("product_detail", "ProductDetail")]
    [InlineData("catalog:products", "CatalogProducts")]
    [InlineData("global cache", "GlobalCache")]
    [InlineData("--double--dash--", "DoubleDash")]
    public void GenerateConstantName_PolicyName_IsPascalCasedPerSeparator(
        string policyName,
        string expected)
    {
        Assert.Equal(expected, OutputCachePoliciesExtractor.GenerateConstantName(policyName));
    }

    [Fact]
    public void Extract_TheTags_GetConstantsOfTheirOwn()
    {
        // A tag is what a host evicts by (IOutputCacheStore.EvictByTagAsync): a constant per tag, once each, sorted;
        // two tags that give one identifier are told apart by a number
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /products:
                                x-cache-type: output
                                x-cache-policy: products
                                x-cache-tags:
                                  - products
                                  - usb-devices
                                get:
                                  operationId: listProducts
                                  responses:
                                    '200':
                                      description: OK
                              /products/{productId}:
                                x-cache-type: output
                                x-cache-policy: product-detail
                                x-cache-tags:
                                  - product-detail
                                  - products
                                  - usb_devices
                                parameters:
                                  - name: productId
                                    in: path
                                    required: true
                                    schema:
                                      type: string
                                get:
                                  operationId: getProduct
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var result = OutputCachePoliciesExtractor.Extract(OpenApiDocumentHelper.ParseYaml(yaml), "TestApi");

        Assert.NotNull(result);
        Assert.Contains("public static class OutputCacheTags", result, StringComparison.Ordinal);
        Assert.Contains("public const string ProductDetail = \"product-detail\";", result, StringComparison.Ordinal);
        Assert.Contains("public const string UsbDevices = \"usb-devices\";", result, StringComparison.Ordinal);
        Assert.Contains("public const string UsbDevices2 = \"usb_devices\";", result, StringComparison.Ordinal);
        var tags = result[result.IndexOf("public static class OutputCacheTags", StringComparison.Ordinal)..];
        Assert.Equal(2, tags.Split("= \"products\";").Length);

        var dependencyInjection = OutputCacheDependencyInjectionExtractor.Extract(OpenApiDocumentHelper.ParseYaml(yaml), "TestApi");
        Assert.NotNull(dependencyInjection);
        Assert.Contains("policy.Tag(OutputCacheTags.ProductDetail, OutputCacheTags.Products, OutputCacheTags.UsbDevices2);", dependencyInjection, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_NoTags_NoTagsClass()
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
                                get:
                                  operationId: listProducts
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var result = OutputCachePoliciesExtractor.Extract(OpenApiDocumentHelper.ParseYaml(yaml), "TestApi");

        Assert.NotNull(result);
        Assert.DoesNotContain("OutputCacheTags", result, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateConstantName_MatchesTheConstantEmittedByExtract()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test
                              version: 1.0.0
                            paths:
                              /products:
                                x-cache-policy: product-list
                                x-cache-expiration-seconds: 60
                                get:
                                  operationId: listProducts
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var constantName = OutputCachePoliciesExtractor.GenerateConstantName("product-list");
        var result = OutputCachePoliciesExtractor.Extract(document, "TestApi");

        Assert.Equal("ProductList", constantName);
        Assert.NotNull(result);
        Assert.Contains($"public const string {constantName} = \"product-list\";", result, StringComparison.Ordinal);
    }
}