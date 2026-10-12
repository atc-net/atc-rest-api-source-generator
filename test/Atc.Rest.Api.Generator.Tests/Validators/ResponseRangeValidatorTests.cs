namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A response declared for a status range (<c>2XX</c>, <c>4XX</c>, <c>5XX</c> - the Microsoft Graph style) names no
/// status the generated code could answer or read with. The range is generated as its first status (<c>2XX</c> as
/// 200) when the operation does not declare that status, and dropped when it does; a warning
/// (<c>ATC_API_OPR034</c>) under every strategy says which.
/// </summary>
public class ResponseRangeValidatorTests
{
    private const string TestFilePath = "ranges.yaml";

    [Theory]
    [InlineData("2XX", "200")]
    [InlineData("4XX", "400")]
    [InlineData("5XX", "500")]
    [InlineData("2xx", "200")]
    public void Validate_AStatusRange_IsAWarningNamingTheStatusItBecomes_UnderEveryStrategy(
        string range,
        string status)
    {
        var document = Parse($"'{range}': {{ description: Any }}");

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var warning = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.ResponseStatusRange);
            Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
            Assert.Contains("listThings", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"'{range}'", warning.Message, StringComparison.Ordinal);
            Assert.Contains($"generated as {status}", warning.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Validate_AStatusRangeNextToItsFirstStatus_IsAWarningSayingItIsDropped()
    {
        var document = Parse("'200': { description: OK }\n        '2XX': { description: Any }");

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.None, document, [], TestFilePath);

        var warning = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.ResponseStatusRange);
        Assert.Contains("'2XX'", warning.Message, StringComparison.Ordinal);
        Assert.Contains("200 is declared", warning.Message, StringComparison.Ordinal);
        Assert.Contains("not generated", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ExplicitStatuses_AreNoWarning()
    {
        var document = Parse("'200': { description: OK }\n        '404': { description: Gone }\n        default: { description: Else }");

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ResponseStatusRange);
    }

    /// <summary>
    /// A parsed document is cached and rewritten in place after its validation: a second pass over the same
    /// specification (the server and the client generator of one build, an IDE re-run) validates the rewritten
    /// document - and still warns, with the same words.
    /// </summary>
    [Theory]
    [InlineData("'2XX': { description: Any }")]
    [InlineData("'200': { description: OK }\n        '2XX': { description: Any }")]
    public void Validate_AfterTheDocumentIsNormalized_StillWarnsTheSame(
        string responses)
    {
        var document = Parse(responses);
        var before = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, document, [], TestFilePath)
            .Where(d => d.RuleId == RuleIdentifiers.ResponseStatusRange).Select(d => d.Message).ToList();

        ResponseRangeNormalizer.WithExplicitStatuses(document);
        ResponseRangeNormalizer.WithExplicitStatuses(document);
        var after = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, document, [], TestFilePath)
            .Where(d => d.RuleId == RuleIdentifiers.ResponseStatusRange).Select(d => d.Message).ToList();

        Assert.Single(before);
        Assert.Equal(before, after);
    }

    private static OpenApiDocument Parse(string responses)
    {
        var yaml = $$"""
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    {{responses}}
            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, TestFilePath);
        Assert.NotNull(document);
        return document;
    }
}