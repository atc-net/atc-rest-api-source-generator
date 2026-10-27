namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// Every generator looks a body up by its canonical media type, so two declarations that become one
/// (<c>application/json</c> and <c>application/json; charset=utf-8</c>, or a model under <c>*/*</c>) keep the first.
/// When the second says something else - another schema - a warning (<c>ATC_API_OPR040</c>) under every strategy names
/// where, what is generated and what is not.
/// </summary>
[RuleTest("ATC_API_OPR040")]
public class MediaTypeDeclaredTwiceValidatorTests
{
    private const string TestFilePath = "twice.yaml";

    [Fact]
    public void Validate_AMediaTypeDeclaredTwiceWithAnotherSchema_IsAWarning_UnderEveryStrategy_BeforeAndAfterTheRewrite()
    {
        var document = Parse("{ $ref: '#/components/schemas/Cat' }");

        foreach (var strategy in (ValidateSpecificationStrategy[])[ValidateSpecificationStrategy.None, ValidateSpecificationStrategy.Standard, ValidateSpecificationStrategy.Strict])
        {
            var warning = Assert.Single(OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath), d => d.RuleId == RuleIdentifiers.MediaTypeDeclaredTwice);
            Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
            Assert.StartsWith("The 200 response of operation 'getPet' declares", warning.Message, StringComparison.Ordinal);
            Assert.Contains("'application/json; charset=utf-8'", warning.Message, StringComparison.Ordinal);
            Assert.Contains("'application/json'", warning.Message, StringComparison.Ordinal);
        }

        var before = Messages(document);
        MediaTypeNormalizer.WithCanonicalMediaTypes(document);

        Assert.Equal(before, Messages(document));
    }

    [Fact]
    public void Validate_AMediaTypeDeclaredTwiceWithTheSameSchema_IsNoWarning()
    {
        var document = Parse("{ $ref: '#/components/schemas/Pet' }");

        Assert.Empty(Messages(document));
    }

    /// <summary>
    /// The same holds for a schema written out twice (what a Java generator emits for its two JSON media types): the
    /// same schema is no warning, whether a reference or inline - one that differs in anything is.
    /// </summary>
    [Theory]
    [InlineData("{ type: object, properties: { name: { type: string } } }", "{ type: object, properties: { name: { type: string } } }", false)]
    [InlineData("{ type: object, properties: { name: { type: string } } }", "{ type: object, properties: { name: { type: integer } } }", true)]
    [InlineData("{ type: array, items: { $ref: '#/components/schemas/Pet' } }", "{ type: array, items: { $ref: '#/components/schemas/Pet' } }", false)]
    [InlineData("{ type: array, items: { $ref: '#/components/schemas/Pet' } }", "{ type: array, items: { $ref: '#/components/schemas/Cat' } }", true)]
    [InlineData("{ $ref: '#/components/schemas/Pet' }", "{ type: object, properties: { name: { type: string } } }", true)]
    public void Validate_AnInlineSchemaDeclaredTwice_IsAWarningOnlyWhenItDiffers(
        string firstSchema,
        string secondSchema,
        bool warns)
    {
        var document = Parse(secondSchema, firstSchema);

        Assert.Equal(warns ? 1 : 0, Messages(document).Count);
    }

    private static List<string> Messages(OpenApiDocument document)
        => OpenApiDocumentValidator.Validate(ValidateSpecificationStrategy.Standard, document, [], TestFilePath)
            .Where(d => d.RuleId == RuleIdentifiers.MediaTypeDeclaredTwice)
            .Select(d => d.Message)
            .ToList();

    private static OpenApiDocument Parse(
        string secondSchema,
        string firstSchema = "{ $ref: '#/components/schemas/Pet' }")
    {
        var yaml = $$"""
            openapi: 3.0.3
            info: { title: Contoso, version: 1.0.0 }
            paths:
              /pets/{id}:
                get:
                  operationId: getPet
                  parameters:
                    - { name: id, in: path, required: true, schema: { type: string } }
                  responses:
                    '200':
                      description: OK
                      content:
                        application/json:
                          schema: {{firstSchema}}
                        application/json; charset=utf-8:
                          schema: {{secondSchema}}
            components:
              schemas:
                Pet: { type: object, properties: { name: { type: string } } }
                Cat: { type: object, properties: { lives: { type: integer } } }
            """;

        // A path of its own: the parser caches a document per path, and a test rewrites its document
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yaml, $"twice-{Guid.NewGuid():N}.yaml");
        Assert.NotNull(document);
        return document;
    }
}