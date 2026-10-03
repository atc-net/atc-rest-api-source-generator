namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for the constant naming of <see cref="RateLimitPoliciesExtractor"/>.
/// The Extract method is covered by <see cref="PolicyExtractorTests"/>.
/// </summary>
public class RateLimitPoliciesExtractorTests
{
    [Theory]
    [InlineData("global", "Global")]
    [InlineData("pets-read", "PetsRead")]
    [InlineData("pets_write", "PetsWrite")]
    [InlineData("api:pets", "ApiPets")]
    [InlineData("per user", "PerUser")]
    public void GenerateConstantName_PolicyName_IsPascalCasedPerSeparator(
        string policyName,
        string expected)
    {
        Assert.Equal(expected, RateLimitPoliciesExtractor.GenerateConstantName(policyName));
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
                              /pets:
                                get:
                                  operationId: listPets
                                  x-ratelimit-policy: pets-read
                                  x-ratelimit-permit-limit: 10
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var constantName = RateLimitPoliciesExtractor.GenerateConstantName("pets-read");
        var result = RateLimitPoliciesExtractor.Extract(document, "TestApi");

        Assert.Equal("PetsRead", constantName);
        Assert.NotNull(result);
        Assert.Contains($"public const string {constantName} = \"pets-read\";", result, StringComparison.Ordinal);
    }
}