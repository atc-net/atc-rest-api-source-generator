namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// ATC_API_SCH022 — an object schema with properties but no <c>required</c> list.
/// </summary>
/// <remarks>
/// Every property of such a schema is optional, so each one is generated nullable with a <c>= null</c> default. That is
/// correct for what the schema says, but usually a gap in the specification: the API always sends some of them. An Info
/// in Strict mode points at the fix - list those properties in <c>required</c>.
/// </remarks>
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

    [Fact]
    public void Informs_OnlyAboutAnObjectSchemaWithPropertiesButNoRequiredList()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(Yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Strict, doc, [], TestFilePath)
            .Where(d => d.RuleId == RuleIdentifiers.SchemaWithoutRequiredProperties)
            .ToList();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Contains("'Item'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("required", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsQuiet_InStandardMode()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(Yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, doc, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.SchemaWithoutRequiredProperties);
    }
}