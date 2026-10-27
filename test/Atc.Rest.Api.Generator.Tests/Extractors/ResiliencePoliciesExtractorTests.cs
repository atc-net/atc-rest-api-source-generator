namespace Atc.Rest.Api.Generator.Tests.Extractors;

/// <summary>
/// Tests for the constant naming of <see cref="ResiliencePoliciesExtractor"/>.
/// The Extract method is covered by <see cref="DependencyInjectionExtractorTests"/>.
/// </summary>
public class ResiliencePoliciesExtractorTests
{
    [Theory]
    [InlineData("standard", "Standard")]
    [InlineData("fast-retry", "FastRetry")]
    [InlineData("slow_retry", "SlowRetry")]
    [InlineData("external:payments", "ExternalPayments")]
    [InlineData("no retry", "NoRetry")]
    public void GenerateConstantName_PolicyName_IsPascalCasedPerSeparator(
        string policyName,
        string expected)
    {
        Assert.Equal(expected, ResiliencePoliciesExtractor.GenerateConstantName(policyName));
    }

    [Fact]
    public void GenerateConstantName_MatchesTheConstantEmittedByExtract()
    {
        const string yaml = """
                            openapi: 3.1.1
                            info:
                              title: Test
                              version: 1.0.0
                            x-retry-policy: fast-retry
                            x-retry-max-attempts: 3
                            x-retry-backoff: exponential
                            paths:
                              /health:
                                get:
                                  operationId: getHealth
                                  responses:
                                    '200':
                                      description: OK
                            """;

        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var constantName = ResiliencePoliciesExtractor.GenerateConstantName("fast-retry");
        var result = ResiliencePoliciesExtractor.Extract(document, "TestApi");

        Assert.Equal("FastRetry", constantName);
        Assert.NotNull(result);
        Assert.Contains($"public const string {constantName} = \"fast-retry\";", result, StringComparison.Ordinal);
    }
}