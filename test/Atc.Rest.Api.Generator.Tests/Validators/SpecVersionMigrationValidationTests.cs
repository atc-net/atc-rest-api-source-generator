namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// ATC_API_VER001-004 — constructs whose meaning depends on the declared OpenAPI version.
/// </summary>
/// <remarks>
/// These rules exist for the author who raises <c>openapi:</c> from 3.0 to 3.1 and expects the rest
/// of the document to keep meaning what it meant. Most of it does; a handful of constructs quietly
/// do not, and the generated code changes without anything failing to compile.
/// <para>
/// They read the raw specification text rather than the parsed document, because the parser discards
/// the very thing they are looking for: in a 3.0 document <c>nullable</c> on a schema with no
/// <c>type</c> is consumed and then dropped, so it reaches neither the type flags nor
/// <c>UnrecognizedKeywords</c>.
/// </para>
/// </remarks>
public class SpecVersionMigrationValidationTests
{
    private const string TestFilePath = "test.yaml";

    [Fact]
    public void Ver001_Warns_WhenNullableSitsBesideOneOfWithNoType()
    {
        // The shape that silently stopped being nullable when the parser was brought in line with
        // the specification. `nullable` has no `type` to modify, so per spec it never applied.
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    billingAccount:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/IdName'
                                IdName:
                                  type: object
                                  properties:
                                    id:
                                      type: string
                            """;

        var diagnostics = Validate(yaml);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.NullableWithoutSiblingType);
    }

    [Fact]
    public void Ver001_ReportsTheLineTheNullableIsOn()
    {
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    billingAccount:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/IdName'
                                IdName:
                                  type: object
                            """;

        var diagnostic = Assert.Single(
            Validate(yaml),
            d => d.RuleId == RuleIdentifiers.NullableWithoutSiblingType);

        Assert.Equal(12, diagnostic.LineNumber);
    }

    [Fact]
    public void Ver001_Silent_WhenNullableHasASiblingType()
    {
        // Valid OpenAPI 3.0. This is the overwhelmingly common spelling and must never be flagged,
        // or the rule is noise on every existing specification.
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                      nullable: true
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.NullableWithoutSiblingType);
    }

    [Fact]
    public void Ver001_Silent_WhenTypePrecedesNullableAcrossOtherKeywords()
    {
        // `type` and `nullable` are siblings but not adjacent. Scanning only the neighbouring line
        // would miss this and report a false positive.
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    tags:
                                      type: array
                                      description: Free-form labels
                                      items:
                                        type: string
                                      nullable: true
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.NullableWithoutSiblingType);
    }

    [Fact]
    public void Ver001_Silent_WhenTheNullableIsCommentedOut()
    {
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    billingAccount:
                                      # nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/IdName'
                                IdName:
                                  type: object
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.NullableWithoutSiblingType);
    }

    [Fact]
    public void Ver001_CarriesTheFixAsSuggestionsAndLinksToTheWiki()
    {
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    billingAccount:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/IdName'
                                IdName:
                                  type: object
                            """;

        var diagnostic = Assert.Single(
            Validate(yaml),
            d => d.RuleId == RuleIdentifiers.NullableWithoutSiblingType);

        Assert.NotNull(diagnostic.Suggestions);
        Assert.NotEmpty(diagnostic.Suggestions);

        // The author needs to see the replacement, not just be told the declaration is inert.
        Assert.Contains(diagnostic.Suggestions, s => s.Contains("oneOf", StringComparison.Ordinal));
        Assert.NotNull(diagnostic.DocumentationUrl);
        Assert.Contains("atc-rest-api-source-generator", diagnostic.DocumentationUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Ver001_Silent_WhenTheCallerHasNoSourceText()
    {
        // The CLI and the generators always have the text, but the overload without it must stay
        // usable rather than throw or guess.
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    billingAccount:
                                      nullable: true
                                      oneOf:
                                        - $ref: '#/components/schemas/IdName'
                                IdName:
                                  type: object
                            """;

        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        var diagnostics = OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.NullableWithoutSiblingType);
    }

    [Fact]
    public void Ver002_Warns_WhenA31DocumentStillUsesNullable()
    {
        // The half-migrated document: the version header was raised, the properties were not. The
        // generator still honours the keyword, so nothing breaks here - but no other tool will.
        const string yaml = """
                            openapi: "3.1.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                      nullable: true
                            """;

        var diagnostics = Validate(yaml);

        Assert.Contains(diagnostics, d => d.RuleId == RuleIdentifiers.NullableKeywordRemovedInSpecVersion);
    }

    [Fact]
    public void Ver002_ReportsOnceForTheWholeDocumentAndCountsTheDeclarations()
    {
        // A specification carries hundreds of these. One diagnostic naming the count is actionable;
        // three hundred identical ones are a wall the author scrolls past.
        const string yaml = """
                            openapi: "3.1.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                      nullable: true
                                    alias:
                                      type: string
                                      nullable: true
                                    note:
                                      type: string
                                      nullable: true
                            """;

        var diagnostic = Assert.Single(
            Validate(yaml),
            d => d.RuleId == RuleIdentifiers.NullableKeywordRemovedInSpecVersion);

        Assert.Contains("'nullable' 3 time(s)", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(13, diagnostic.LineNumber);
    }

    [Fact]
    public void Ver002_Silent_OnA30Document()
    {
        // Valid where it is declared. Telling a 3.0 author to migrate on every build is noise, and
        // the advice only becomes actionable once they raise the version themselves.
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                      nullable: true
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.NullableKeywordRemovedInSpecVersion);
    }

    [Fact]
    public void Ver002_Silent_WhenA31DocumentUsesTheTypeArrayForm()
    {
        const string yaml = """
                            openapi: "3.1.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    name:
                                      type: [string, "null"]
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.NullableKeywordRemovedInSpecVersion);
    }

    [Fact]
    public void Ver002_ShowsTheReplacementSpelling()
    {
        const string yaml = """
                            openapi: "3.1.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                      nullable: true
                            """;

        var diagnostic = Assert.Single(
            Validate(yaml),
            d => d.RuleId == RuleIdentifiers.NullableKeywordRemovedInSpecVersion);

        Assert.NotNull(diagnostic.Suggestions);
        Assert.Contains(diagnostic.Suggestions, s => s.Contains("\"null\"", StringComparison.Ordinal));
        Assert.NotNull(diagnostic.DocumentationUrl);
    }

    [Fact]
    public void Ver003_Warns_WhenA31DocumentUsesTheBooleanExclusiveForm()
    {
        // In 3.0 the pair means "maximum 10, exclusive". In 3.1 'exclusiveMaximum' carries the bound
        // itself, so a boolean there is not a bound at all and the constraint is lost.
        const string yaml = """
                            openapi: "3.1.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Reading:
                                  type: object
                                  properties:
                                    value:
                                      type: number
                                      maximum: 10
                                      exclusiveMaximum: true
                            """;

        var diagnostic = Assert.Single(
            Validate(yaml),
            d => d.RuleId == RuleIdentifiers.ExclusiveBoundBooleanForm);

        Assert.Equal(14, diagnostic.LineNumber);
        Assert.Contains("exclusiveMaximum: 10", diagnostic.Suggestions!, StringComparer.Ordinal);
    }

    [Fact]
    public void Ver003_Silent_WhenA31DocumentUsesTheNumericForm()
    {
        const string yaml = """
                            openapi: "3.1.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Reading:
                                  type: object
                                  properties:
                                    value:
                                      type: number
                                      exclusiveMaximum: 10
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ExclusiveBoundBooleanForm);
    }

    [Fact]
    public void Ver003_Silent_OnA30Document()
    {
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Reading:
                                  type: object
                                  properties:
                                    value:
                                      type: number
                                      maximum: 10
                                      exclusiveMaximum: true
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ExclusiveBoundBooleanForm);
    }

    [Fact]
    public void Ver003_Silent_WhenTheBooleanIsFalse()
    {
        // 'exclusiveMinimum: false' is the 3.0 default spelled out. It changes nothing, and the
        // 3.1 rewrite is simply to delete it - not worth a diagnostic of its own.
        const string yaml = """
                            openapi: "3.1.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Reading:
                                  type: object
                                  properties:
                                    value:
                                      type: number
                                      minimum: 1
                                      exclusiveMinimum: false
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.ExclusiveBoundBooleanForm);
    }

    [Fact]
    public void Ver004_Warns_WhenA30DocumentUsesATypeArray()
    {
        // The migration done in the wrong order: the properties were rewritten in the 3.1 spelling
        // while the version header still says 3.0, where 'type' must be a single string.
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    name:
                                      type: [string, "null"]
                            """;

        var diagnostic = Assert.Single(
            Validate(yaml),
            d => d.RuleId == RuleIdentifiers.TypeArrayNotSupportedInSpecVersion);

        Assert.Equal(12, diagnostic.LineNumber);
        Assert.Contains("3.1", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ver004_Silent_WhenTheSameDocumentIsDeclaredAs31()
    {
        const string yaml = """
                            openapi: "3.1.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    name:
                                      type: [string, "null"]
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.TypeArrayNotSupportedInSpecVersion);
    }

    [Fact]
    public void Ver004_Silent_WhenA30DocumentUsesASingleType()
    {
        const string yaml = """
                            openapi: "3.0.1"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Account:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                            """;

        var diagnostics = Validate(yaml);

        Assert.DoesNotContain(diagnostics, d => d.RuleId == RuleIdentifiers.TypeArrayNotSupportedInSpecVersion);
    }

    private static IReadOnlyList<DiagnosticMessage> Validate(string yaml)
    {
        var doc = OpenApiDocumentHelper.ParseYaml(yaml);

        return OpenApiDocumentValidator.Validate(
            ValidateSpecificationStrategy.Strict, doc, [], TestFilePath, yaml);
    }
}