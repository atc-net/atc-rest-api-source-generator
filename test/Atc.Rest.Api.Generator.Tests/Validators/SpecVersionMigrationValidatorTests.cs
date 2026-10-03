namespace Atc.Rest.Api.Generator.Tests.Validators;

/// <summary>
/// <see cref="SpecVersionMigrationValidator.Validate"/> called directly: the entry point's own contract
/// (what it needs to run, which rule set the declared version selects) rather than the rules' details,
/// which <see cref="SpecVersionMigrationValidationTests"/> cover through the document validator.
/// </summary>
public class SpecVersionMigrationValidatorTests
{
    private const string FilePath = "spec.yaml";

    private const string NullableWithoutTypeYaml30 = """
        openapi: "3.0.3"
        info:
          title: Test API
          version: "1.0.0"
        paths: {}
        components:
          schemas:
            Account:
              type: object
              properties:
                owner:
                  nullable: true
                  oneOf:
                    - $ref: '#/components/schemas/Owner'
            Owner:
              type: object
        """;

    [Fact]
    public void Validate_NullSourceText_AddsNothing()
    {
        var document = OpenApiDocumentHelper.ParseYaml(NullableWithoutTypeYaml30);
        var diagnostics = new List<DiagnosticMessage>();

        SpecVersionMigrationValidator.Validate(diagnostics, FilePath, document, sourceText: null);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Validate_EmptySourceText_AddsNothing()
    {
        var document = OpenApiDocumentHelper.ParseYaml(NullableWithoutTypeYaml30);
        var diagnostics = new List<DiagnosticMessage>();

        SpecVersionMigrationValidator.Validate(diagnostics, FilePath, document, string.Empty);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Validate_OpenApi30NullableWithoutSiblingType_AddsVer001AtTheLine()
    {
        var document = OpenApiDocumentHelper.ParseYaml(NullableWithoutTypeYaml30);
        var existing = DiagnosticMessage.Info("ATC_API_XXX", "already there");
        var diagnostics = new List<DiagnosticMessage> { existing };

        SpecVersionMigrationValidator.Validate(diagnostics, FilePath, document, NullableWithoutTypeYaml30);

        Assert.Equal(2, diagnostics.Count);
        Assert.Same(existing, diagnostics[0]);
        var diagnostic = diagnostics[1];
        Assert.Equal(RuleIdentifiers.NullableWithoutSiblingType, diagnostic.RuleId);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(FilePath, diagnostic.FilePath);
        Assert.Equal(12, diagnostic.LineNumber);
        Assert.Equal("nullable: true", diagnostic.Context);
        Assert.StartsWith("'nullable' is declared on a schema that has no 'type' of its own", diagnostic.Message, StringComparison.Ordinal);
        Assert.NotNull(diagnostic.Suggestions);
        Assert.Equal(3, diagnostic.Suggestions.Count);
        Assert.Equal(Constants.Documentation.GetRuleUrl(RuleIdentifiers.NullableWithoutSiblingType), diagnostic.DocumentationUrl);
    }

    [Fact]
    public void Validate_OpenApi30TypeArray_AddsVer004()
    {
        const string yaml = """
                            openapi: "3.0.3"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: [string, "null"]
                            """;
        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var diagnostics = new List<DiagnosticMessage>();

        SpecVersionMigrationValidator.Validate(diagnostics, FilePath, document, yaml);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.TypeArrayNotSupportedInSpecVersion, diagnostic.RuleId);
        Assert.Equal(12, diagnostic.LineNumber);
        Assert.Equal("type: [string, \"null\"]", diagnostic.Context);
        Assert.NotNull(diagnostic.Suggestions);
        Assert.Contains(diagnostic.Suggestions, s => s.StartsWith("Raise the document to 'openapi: 3.1.0'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_OpenApi31NullableKeyword_AddsOneVer002ForTheWholeDocument()
    {
        const string yaml = """
                            openapi: "3.1.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                      nullable: true
                                    nickname:
                                      type: string
                                      nullable: true
                            """;
        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var diagnostics = new List<DiagnosticMessage>();

        SpecVersionMigrationValidator.Validate(diagnostics, FilePath, document, yaml);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.NullableKeywordRemovedInSpecVersion, diagnostic.RuleId);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(13, diagnostic.LineNumber);
        Assert.StartsWith("The document declares 'nullable' 2 time(s)", diagnostic.Message, StringComparison.Ordinal);
        Assert.EndsWith("The first declaration is on line 13.", diagnostic.Message, StringComparison.Ordinal);
        Assert.NotNull(diagnostic.Suggestions);
        Assert.Contains(diagnostic.Suggestions, s => s.Contains("ATC_API_VER002", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_OpenApi31BooleanExclusiveMaximum_AddsVer003WithTheNumericSpelling()
    {
        const string yaml = """
                            openapi: "3.1.0"
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Order:
                                  type: object
                                  properties:
                                    quantity:
                                      type: integer
                                      maximum: 10
                                      exclusiveMaximum: true
                            """;
        var document = OpenApiDocumentHelper.ParseYaml(yaml);
        var diagnostics = new List<DiagnosticMessage>();

        SpecVersionMigrationValidator.Validate(diagnostics, FilePath, document, yaml);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(RuleIdentifiers.ExclusiveBoundBooleanForm, diagnostic.RuleId);
        Assert.Equal(14, diagnostic.LineNumber);
        Assert.Equal("exclusiveMaximum: true", diagnostic.Context);
        Assert.Contains("'maximum' endpoint", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(
            [
                "exclusiveMaximum: 10",
                "Remove the 'maximum: 10' line - the bound now lives on 'exclusiveMaximum'.",
            ],
            diagnostic.Suggestions);
    }

    [Fact]
    public void Validate_RulesFollowTheDeclaredVersion_NotTheConstruct()
    {
        // The 3.0 rules do not run on a 3.1 document and vice versa: the same 'nullable' beside a
        // 'type' is fine at 3.0 and VER002 at 3.1; a type array is VER004 at 3.0 and fine at 3.1.
        const string body = """
                            info:
                              title: Test API
                              version: "1.0.0"
                            paths: {}
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    name:
                                      type: string
                                      nullable: true
                                    tags:
                                      type: [array, "null"]
                                      items:
                                        type: string
                            """;
        var yaml30 = "openapi: \"3.0.3\"\n" + body;
        var yaml31 = "openapi: \"3.1.0\"\n" + body;
        var diagnostics30 = new List<DiagnosticMessage>();
        var diagnostics31 = new List<DiagnosticMessage>();

        SpecVersionMigrationValidator.Validate(diagnostics30, FilePath, OpenApiDocumentHelper.ParseYaml(yaml30), yaml30);
        SpecVersionMigrationValidator.Validate(diagnostics31, FilePath, OpenApiDocumentHelper.ParseYaml(yaml31), yaml31);

        Assert.Equal([RuleIdentifiers.TypeArrayNotSupportedInSpecVersion], diagnostics30.Select(d => d.RuleId), StringComparer.Ordinal);
        Assert.Equal([RuleIdentifiers.NullableKeywordRemovedInSpecVersion], diagnostics31.Select(d => d.RuleId), StringComparer.Ordinal);
    }
}