namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// ATC_API_SCH022 — an object schema with properties but no <c>required</c> list.
/// </summary>
/// <remarks>
/// Every property of such a schema is optional, so each one is generated nullable with a <c>= null</c> default. That is
/// correct for what the schema says, but usually a gap in the specification: the API always sends some of them. It is
/// the most common reason code written against a 2.x model no longer compiles, so it is a warning under every strategy -
/// a build shows it - pointing at the fix: list those properties in <c>required</c>.
/// </remarks>
[RuleTest("ATC_API_SCH022")]
public class SchemaWithoutRequiredValidationTests
{
    private const string TestFilePath = "test.yaml";

    private const string Yaml = """
                                openapi: 3.0.3
                                info:
                                  title: Test API
                                  version: 1.0.0
                                paths: {}
                                components:
                                  schemas:
                                    Item:
                                      type: object
                                      title: Item
                                      properties:
                                        id:
                                          type: string
                                        name:
                                          type: string
                                    Pet:
                                      type: object
                                      title: Pet
                                      required:
                                        - id
                                      properties:
                                        id:
                                          type: string
                                        tag:
                                          type: string
                                    Empty:
                                      type: object
                                      title: Empty
                                    Colour:
                                      type: string
                                      title: Colour
                                      enum:
                                        - Red
                                        - Blue
                                """;

    [Theory]
    [InlineData(ValidateSpecificationStrategy.None)]
    [InlineData(ValidateSpecificationStrategy.Standard)]
    [InlineData(ValidateSpecificationStrategy.Strict)]
    public void Warns_OnlyAboutAnObjectSchemaWithPropertiesButNoRequiredList_UnderEveryStrategy(
        ValidateSpecificationStrategy strategy)
    {
        var doc = OpenApiDocumentHelper.ParseYaml(Yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(strategy, doc, [], TestFilePath)
            .Where(d => d.RuleId == RuleIdentifiers.SchemaWithoutRequiredProperties)
            .ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("'Item'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("required", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsSwitchedOff_ByXAtcSuppressOnTheSchema()
    {
        // Item says its properties are optional on purpose; Filter, a copy of it, does not
        var doc = OpenApiDocumentHelper.ParseYaml(Yaml
            .Replace(
                "    Pet:",
                "    Filter:\n      type: object\n      title: Filter\n      properties:\n        name:\n          type: string\n    Pet:",
                StringComparison.Ordinal)
            .Replace(
                "title: Item",
                "title: Item\n      x-atc-suppress: [ATC_API_SCH022]",
                StringComparison.Ordinal));

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, doc, [], TestFilePath)
            .Where(d => d.RuleId == RuleIdentifiers.SchemaWithoutRequiredProperties)
            .ToList();

        var reported = Assert.Single(diagnostics);
        Assert.Contains("'Filter'", reported.Message, StringComparison.Ordinal);
    }
}