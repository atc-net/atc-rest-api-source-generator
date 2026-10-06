namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// A component schema that contains itself - through <c>allOf</c> (it inherits itself), through a bare
/// <c>$ref</c> (it aliases itself) or through the <c>items</c> of an array alias (it lists itself) - cannot be
/// generated: the extractors inline those three, so they would recurse without end. The validator reports the cycle
/// (<c>ATC_API_SCH023</c>) and names it. A schema that refers to itself from a property is a tree, which is fine.
/// </summary>
public class SchemaCycleValidatorTests
{
    private const string TestFilePath = "cycles.yaml";

    private const string Head = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/A'
        components:
          schemas:

        """;

    public static TheoryData<string, string, string> Cycles
        => new()
        {
            {
                "allOf of itself",
                """
                    A:
                      allOf:
                        - $ref: '#/components/schemas/A'
                        - type: object
                          properties: { name: { type: string } }
                """,
                "A -> A"
            },
            {
                "allOf of each other",
                """
                    A:
                      allOf:
                        - $ref: '#/components/schemas/B'
                    B:
                      allOf:
                        - $ref: '#/components/schemas/A'
                """,
                "A -> B -> A"
            },
            {
                "an array alias of itself",
                """
                    A:
                      type: array
                      items:
                        $ref: '#/components/schemas/A'
                """,
                "A -> A"
            },
            {
                "array aliases of each other",
                """
                    A:
                      type: array
                      items:
                        $ref: '#/components/schemas/B'
                    B:
                      type: array
                      items:
                        $ref: '#/components/schemas/A'
                """,
                "A -> B -> A"
            },
            {
                "a bare $ref to itself",
                """
                    A:
                      $ref: '#/components/schemas/A'
                """,
                "A -> A"
            },
            {
                "bare $refs to each other",
                """
                    A:
                      $ref: '#/components/schemas/B'
                    B:
                      $ref: '#/components/schemas/A'
                """,
                "A -> B -> A"
            },
            {
                "allOf through an inline array alias",
                """
                    A:
                      allOf:
                        - type: array
                          items:
                            $ref: '#/components/schemas/A'
                """,
                "A -> A"
            },
        };

    public static TheoryData<string, string> Trees
        => new()
        {
            {
                "a property of its own type",
                """
                    A:
                      type: object
                      properties:
                        parent:
                          $ref: '#/components/schemas/A'
                """
            },
            {
                "an array property of its own type",
                """
                    A:
                      type: object
                      properties:
                        children:
                          type: array
                          items:
                            $ref: '#/components/schemas/A'
                """
            },
            {
                "the discriminator pattern: oneOf of the variants, a variant allOf the base",
                """
                    A:
                      type: object
                      discriminator: { propertyName: kind }
                      oneOf:
                        - $ref: '#/components/schemas/B'
                    B:
                      allOf:
                        - $ref: '#/components/schemas/A'
                        - type: object
                          properties: { radius: { type: number } }
                """
            },
            {
                "an array alias of a tree",
                """
                    A:
                      type: array
                      items:
                        $ref: '#/components/schemas/B'
                    B:
                      type: object
                      properties:
                        items:
                          $ref: '#/components/schemas/A'
                """
            },
        };

    [Theory]
    [MemberData(nameof(Cycles))]
    public void Validate_ASchemaThatContainsItself_IsOneErrorNamingTheCycle(
        string name,
        string schemas,
        string expectedChain)
    {
        var document = Parse(schemas);

        var diagnostics = SchemaCycleValidator.Validate(document, TestFilePath);

        var error = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.SchemaCycle, error.RuleId);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains(expectedChain, error.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(name));
    }

    [Theory]
    [MemberData(nameof(Trees))]
    public void Validate_ASchemaThatRefersToItselfFromAProperty_IsNoCycle(
        string name,
        string schemas)
    {
        var document = Parse(schemas);

        var diagnostics = SchemaCycleValidator.Validate(document, TestFilePath);

        Assert.Empty(diagnostics);
        Assert.False(string.IsNullOrEmpty(name));
    }

    /// <summary>
    /// The document validator reports the cycle under every strategy, <c>None</c> included, and nothing else: the
    /// later checks (and the generators) would recurse into the cycle.
    /// </summary>
    [Theory]
    [InlineData(ValidateSpecificationStrategy.None)]
    [InlineData(ValidateSpecificationStrategy.Standard)]
    [InlineData(ValidateSpecificationStrategy.Strict)]
    public void DocumentValidator_ASchemaCycle_IsTheOnlyDiagnosticUnderEveryStrategy(
        ValidateSpecificationStrategy strategy)
    {
        var document = Parse(
            """
                A:
                  allOf:
                    - $ref: '#/components/schemas/B'
                B:
                  allOf:
                    - $ref: '#/components/schemas/A'
            """);

        var diagnostics = OpenApiDocumentValidator.Validate(strategy, document, [], TestFilePath);

        var error = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.SchemaCycle, error.RuleId);
    }

    private static OpenApiDocument Parse(string schemas)
    {
        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(Head + schemas, TestFilePath + schemas.Length);
        Assert.NotNull(document);
        return document;
    }
}
