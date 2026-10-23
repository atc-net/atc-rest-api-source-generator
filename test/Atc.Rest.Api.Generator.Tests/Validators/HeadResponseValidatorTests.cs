namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A HEAD answer has no body, so the content a HEAD operation declares for a response (usually copied from its GET)
/// is not generated: no client returns a value for it and the server's result takes none. A warning
/// (<c>ATC_API_OPR039</c>) under every strategy names the operation and the statuses.
/// </summary>
[RuleTest("ATC_API_OPR039")]
public class HeadResponseValidatorTests
{
    private const string TestFilePath = "head.yaml";

    [Fact]
    public void Validate_AHeadOperationWithContent_IsOneWarningNamingItsStatuses_UnderEveryStrategy()
    {
        var document = Parse(
            "head",
            """
            '200':
                      description: OK
                      content:
                        application/json:
                          schema: { type: object }
                    '404':
                      description: Gone
                      content:
                        application/problem+json:
                          schema: { type: object }
                    '500': { description: Failed }
            """);

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var warning = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.HeadResponseContentIgnored);
            Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
            Assert.Contains("'probeThing'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("200, 404", warning.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("500", warning.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A parsed document is cached and rewritten in place after its validation: a second pass over the same
    /// specification validates the rewritten document - and still warns, with the same words.
    /// </summary>
    [Fact]
    public void Validate_AfterTheDocumentIsNormalized_StillWarnsTheSame()
    {
        var document = Parse(
            "head",
            """
            '200':
                      description: Probed
                      content:
                        application/json:
                          schema: { type: object }
            """);
        var before = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, document, [], TestFilePath)
            .Where(d => d.RuleId == RuleIdentifiers.HeadResponseContentIgnored).Select(d => d.Message).ToList();

        HeadResponseNormalizer.WithoutResponseBodies(document);
        HeadResponseNormalizer.WithoutResponseBodies(document);
        var after = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, document, [], TestFilePath)
            .Where(d => d.RuleId == RuleIdentifiers.HeadResponseContentIgnored).Select(d => d.Message).ToList();

        Assert.Single(before);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Validate_AHeadOperationWithoutContent_IsNoWarning()
    {
        var document = Parse("head", "'200': { description: OK }\n        '404': { description: Gone }");

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.HeadResponseContentIgnored);
    }

    [Fact]
    public void Validate_AGetOperationWithContent_IsNoWarning()
    {
        var document = Parse(
            "get",
            """
            '200':
                      description: OK
                      content:
                        application/json:
                          schema: { type: object }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.HeadResponseContentIgnored);
    }

    private static OpenApiDocument Parse(
        string method,
        string responses)
    {
        var yaml = $$"""
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /things:
                {{method}}:
                  operationId: probeThing
                  responses:
                    {{responses}}
            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, TestFilePath);
        Assert.NotNull(document);
        return document;
    }
}