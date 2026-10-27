namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A rule about the specification is reported in the specification file, which is no C# file: an <c>.editorconfig</c>
/// section's <c>dotnet_diagnostic.&lt;id&gt;.severity</c> never reaches it (Roslyn hands a severity to the syntax trees
/// it matches, never to a file's analyzer options), so the marker's <c>ruleSeverities</c> gives a rule its severity for
/// the project, <c>suppressRules</c> switches a rule off, and <c>x-atc-suppress</c> switches it off for one operation,
/// path or schema.
/// </summary>
public class RuleSeverityConfigurationTests
{
    private const string SpecPath = "/project/Things.yaml";

    private const string Yaml = """
        openapi: 3.1.1
        info:
          title: Things
          version: 1.0.0
        paths:
          /things/{id}/snapshot:
            get:
              operationId: captureSnapshot
              x-atc-suppress: [ATC_API_OPR003]
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '204':
                  description: Taken
          /things/{id}:
            get:
              operationId: fetchThing
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: The thing
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Thing'
        components:
          schemas:
            Thing:
              type: object
              title: Thing
              required: [id, state]
              properties:
                id:
                  type: string
                state:
                  $ref: '#/components/schemas/State'
            State:
              type: string
              title: State
              enum: [onLine, offLine, standBy]
            Mode:
              type: string
              title: Mode
              x-atc-suppress: [ATC_API_NAM005]
              enum: [fast_lane]
            Untitled:
              type: object
              x-atc-suppress: [ATC_API_SCH003]
              required: [name]
              properties:
                name:
                  type: string
        """;

    private const string Marker = """{ "validateSpecificationStrategy": "Strict" }""";

    [Fact]
    public void WithoutConfiguration_TheRulesAreReported()
    {
        var diagnostics = Run(Marker);

        // fetchThing is no get/list name; captureSnapshot says so in the spec
        var getPrefix = Assert.Single(diagnostics, d => d.Id == Generator.RuleIdentifiers.GetOperationIdPrefix);
        Assert.Contains("fetchThing", getPrefix.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Equal(DiagnosticSeverity.Warning, getPrefix.Severity);

        // Mode's values break the document's casing, and its schema says so; Untitled has no title on purpose
        Assert.DoesNotContain(diagnostics, d => d.Id == Generator.RuleIdentifiers.EnumValueCasing);
        Assert.DoesNotContain(diagnostics, d => d.Id == Generator.RuleIdentifiers.ObjectTitleMissing);
    }

    [Theory]
    [InlineData("none", null)]
    [InlineData("silent", null)]
    [InlineData("suggestion", DiagnosticSeverity.Info)]
    [InlineData("warning", DiagnosticSeverity.Warning)]
    [InlineData("error", DiagnosticSeverity.Error)]
    public void TheMarkersRuleSeverity_IsTheRulesSeverity(
        string severity,
        DiagnosticSeverity? expected)
    {
        var diagnostics = Run($$"""{ "validateSpecificationStrategy": "Strict", "ruleSeverities": { "{{Generator.RuleIdentifiers.GetOperationIdPrefix}}": "{{severity}}" } }""");

        var reported = diagnostics.Where(d => d.Id == Generator.RuleIdentifiers.GetOperationIdPrefix).ToList();
        if (expected is null)
        {
            Assert.Empty(reported);
        }
        else
        {
            Assert.Equal(expected, Assert.Single(reported).Severity);
        }
    }

    /// <summary>
    /// Why the marker: Roslyn's own <c>AnalyzerConfigSet</c> - what a build reads its <c>.editorconfig</c> and global
    /// configs with - gives a specification file's analyzer options no <c>dotnet_diagnostic.&lt;id&gt;.severity</c>: a
    /// severity goes to the syntax trees a section matches, and a specification file is none.
    /// </summary>
    [Fact]
    public void AnEditorConfigSeverity_IsNoAnalyzerOptionOfTheSpecificationFile()
    {
        var editorConfig = AnalyzerConfig.Parse(
            $$"""
             root = true

             [*.{yaml,yml}]
             dotnet_diagnostic.{{Generator.RuleIdentifiers.GetOperationIdPrefix}}.severity = error
             some_option = some_value
             """,
            "/project/.editorconfig");

        var options = AnalyzerConfigSet.Create(ImmutableArray.Create(editorConfig)).GetOptionsForSourcePath(SpecPath);

        Assert.True(options.AnalyzerOptions.ContainsKey("some_option"));
        Assert.DoesNotContain(options.AnalyzerOptions.Keys, key => key.StartsWith("dotnet_diagnostic.", StringComparison.Ordinal));
        Assert.Equal(ReportDiagnostic.Error, options.TreeOptions[Generator.RuleIdentifiers.GetOperationIdPrefix]);
    }

    [Fact]
    public void TheMarkersRuleSeverities_ReachAMultiPartSpecification()
    {
        var diagnostics = Run(
            $$"""{ "validateSpecificationStrategy": "Strict", "ruleSeverities": { "{{Generator.RuleIdentifiers.GetOperationIdPrefix}}": "error" } }""",
            parts: [("/project/Things_More.yaml", """
                openapi: 3.1.1
                info:
                  title: Things
                  version: 1.0.0
                paths:
                  /others:
                    get:
                      operationId: fetchOthers
                      responses:
                        '204':
                          description: None
                """)]);

        Assert.All(
            diagnostics.Where(d => d.Id == Generator.RuleIdentifiers.GetOperationIdPrefix),
            d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        Assert.Contains(diagnostics, d => d.Id == Generator.RuleIdentifiers.GetOperationIdPrefix && d.GetMessage(CultureInfo.InvariantCulture).Contains("fetchOthers", StringComparison.Ordinal));
    }

    [Fact]
    public void TheMarkersSuppressRules_SwitchARuleOff()
    {
        var diagnostics = Run(
            """{ "validateSpecificationStrategy": "Strict", "suppressRules": ["ATC_API_OPR003"] }""");

        Assert.DoesNotContain(diagnostics, d => d.Id == Generator.RuleIdentifiers.GetOperationIdPrefix);
    }

    /// <summary>
    /// ATC_API_SCH022 is a warning under every strategy (a build shows it); the marker's <c>ruleSeverities</c> brings it
    /// back to an information, which a build does not print.
    /// </summary>
    [Theory]
    [InlineData("""{ "validateSpecificationStrategy": "Standard" }""", DiagnosticSeverity.Warning)]
    [InlineData("""{ "validateSpecificationStrategy": "Standard", "ruleSeverities": { "ATC_API_SCH022": "suggestion" } }""", DiagnosticSeverity.Info)]
    [InlineData("""{ "validateSpecificationStrategy": "Strict", "ruleSeverities": { "ATC_API_SCH022": "suggestion" } }""", DiagnosticSeverity.Info)]
    public void SchemaWithoutRequired_IsAWarning_TheMarkerCanMakeItInfo(
        string marker,
        DiagnosticSeverity expected)
    {
        const string yaml = """
            openapi: 3.1.1
            info:
              title: Things
              version: 1.0.0
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '200':
                      description: The things
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Thing'
            components:
              schemas:
                Thing:
                  type: object
                  title: Thing
                  properties:
                    id:
                      type: string
            """;

        var diagnostics = Run(marker, yaml);

        Assert.Equal(expected, Assert.Single(diagnostics, d => d.Id == "ATC_API_SCH022").Severity);
    }

    [Fact]
    public void AnErrorThatStopsGeneration_IsNotSwitchedOff()
    {
        // An error stands for code the generator cannot write: switching it off would hide why nothing was generated
        const string yaml = """
            openapi: 3.1.1
            info:
              title: Things
              version: 1.0.0
            paths:
              /things:
                get:
                  operationId: listThings
                  responses:
                    '200':
                      description: The things
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Missing'
            """;
        var diagnostics = Run(
            """{ "validateSpecificationStrategy": "Strict", "suppressRules": ["ATC_API_SCH013"], "ruleSeverities": { "ATC_API_SCH013": "none" } }""",
            yaml);

        Assert.Contains(diagnostics, d => d.Id == "ATC_API_SCH013" && d.Severity == DiagnosticSeverity.Error);
    }

    private static List<Diagnostic> Run(
        string marker,
        string yaml = Yaml,
        IReadOnlyList<(string Path, string Yaml)>? parts = null)
    {
        var compilation = CSharpCompilation.Create(
            "Things",
            [],
            CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: false),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(new ApiServerGenerator().AsSourceGenerator())
            .AddAdditionalTexts(
            [
                new CompilationVerificationHarness.InMemoryAdditionalText(SpecPath, yaml),
                .. (parts ?? []).Select(part => new CompilationVerificationHarness.InMemoryAdditionalText(part.Path, part.Yaml)),
                new CompilationVerificationHarness.InMemoryAdditionalText("/project/.atc-rest-api-server", marker),
            ]);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, CancellationToken.None);
        return [.. diagnostics];
    }
}