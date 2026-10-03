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