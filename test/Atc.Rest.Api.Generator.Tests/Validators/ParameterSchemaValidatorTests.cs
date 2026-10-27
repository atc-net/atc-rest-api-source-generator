namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A parameter has a <c>schema</c> or a <c>content</c> map; one with neither has no type, and the clients would
/// pass an <c>object</c> where a string belongs. An error (<c>ATC_API_OPR033</c>) under every strategy, naming the
/// operation and the parameter.
/// </summary>
[RuleTest("ATC_API_OPR033")]
public class ParameterSchemaValidatorTests
{
    private const string TestFilePath = "parameters.yaml";

    [Theory]
    [InlineData("query")]
    [InlineData("header")]
    [InlineData("path")]
    public void Validate_AParameterWithNeitherSchemaNorContent_IsAnError_UnderEveryStrategy(
        string location)
    {
        var document = Parse(
            $$"""
            paths:
              /things/{filter}:
                get:
                  operationId: listThings
                  parameters:
                    - name: filter
                      in: {{location}}
                      required: true
                  responses:
                    '200': { description: OK }
            """);

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

            var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.ParameterWithoutSchema);
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Contains("filter", error.Message, StringComparison.Ordinal);
            Assert.Contains("listThings", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Validate_APathLevelParameterWithoutASchema_IsAnError()
    {
        var document = Parse(
            """
            paths:
              /things:
                parameters:
                  - name: tenant
                    in: header
                get:
                  operationId: listThings
                  responses:
                    '200': { description: OK }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.None, document, [], TestFilePath);

        var error = Assert.Single(diagnostics, d => d.RuleId == RuleIdentifiers.ParameterWithoutSchema);
        Assert.Contains("tenant", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ParametersWithASchemaOrAContentMap_AreNoError()
    {
        var document = Parse(
            """
            paths:
              /things:
                get:
                  operationId: listThings
                  parameters:
                    - name: q
                      in: query
                      schema: { type: string }
                    - name: filter
                      in: query
                      content:
                        application/json:
                          schema: { type: object, properties: { name: { type: string } } }
                  responses:
                    '200': { description: OK }
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, document, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ParameterWithoutSchema);
    }

    private static OpenApiDocument Parse(string body)
    {
        const string head = """
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }

            """;
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(head + body, TestFilePath);
        Assert.NotNull(document);
        return document;
    }
}