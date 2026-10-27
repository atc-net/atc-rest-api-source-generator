namespace Atc.Rest.Api.Generator.Tests.Extractors;

public sealed class LengthLimitAttributesExtractorTests
{
    [Theory]
    [InlineData("{ type: string, pattern: '^[A-Z]{2}$' }", new[] { "ApiRegularExpression" })]
    [InlineData("{ type: array, items: { type: string, pattern: '^[^:]+:.+$' } }", new[] { "ApiItemsRegularExpression" })]
    [InlineData("{ type: string, format: uri }", new[] { "ApiAbsoluteUri" })]
    [InlineData("{ type: string, format: uri-reference }", new string[0])]
    [InlineData("{ $ref: '#/components/schemas/ProductCode' }", new[] { "ApiRegularExpression" })]
    [InlineData("{ type: array, items: { $ref: '#/components/schemas/ProductCode' } }", new[] { "ApiItemsRegularExpression" })]
    [InlineData("{ type: string, format: email, pattern: '^.+@contoso\\.com$' }", new[] { "ApiRegularExpression" })]
    [InlineData("{ type: string, maxLength: 5 }", new string[0])]
    public void NeededValueAttributes_AreTheOnesThePropertiesAreWrittenWith(
        string propertySchema,
        string[] expected)
        => Assert.Equal(expected, LengthLimitAttributesExtractor.NeededValueAttributes(Document(propertySchema)));

    [Theory]
    [InlineData("{ type: string, maxLength: 5 }", true)]
    [InlineData("{ $ref: '#/components/schemas/ProductCode' }", true)]
    [InlineData("{ type: string, format: uri, maxLength: 5 }", false)]
    [InlineData("{ type: string, pattern: '^[A-Z]{2}$' }", false)]
    public void IsNeeded_ForALengthLimit_InPlaceOrThroughAnAlias(
        string propertySchema,
        bool expected)
        => Assert.Equal(expected, LengthLimitAttributesExtractor.IsNeeded(Document(propertySchema)));

    [Fact]
    public void GenerateValueAttributes_DeclaresOnlyTheNeededOnes()
    {
        var source = LengthLimitAttributesExtractor.GenerateValueAttributes("Shop.Generated", ["ApiRegularExpression", "ApiAbsoluteUri"]);

        Assert.Contains("internal sealed class ApiRegularExpressionAttribute : RegularExpressionAttribute", source, StringComparison.Ordinal);
        Assert.Contains("internal sealed class ApiAbsoluteUriAttribute : ValidationAttribute", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiItemsRegularExpressionAttribute", source, StringComparison.Ordinal);
    }

    // A model whose only property has the given schema; ProductCode is a string component with a pattern and a length
    private static OpenApiDocument Document(string propertySchema)
        => OpenApiDocumentHelper.ParseYaml(
            $$"""
              openapi: 3.0.3
              info: { title: Shop, version: 1.0.0 }
              paths: {}
              components:
                schemas:
                  ProductCode: { type: string, minLength: 6, maxLength: 6, pattern: '^[A-Z]{2}\d{4}$' }
                  Order:
                    type: object
                    properties:
                      value: {{propertySchema}}
              """);
}