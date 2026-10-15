namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A rule about the specification is reported in the specification file, which is no C# file: the build's
/// <c>.editorconfig</c> / <c>.globalconfig</c> severities would not reach it, so the generator reads
/// <c>dotnet_diagnostic.&lt;id&gt;.severity</c> for the specification file itself. Beside that, the marker's
/// <c>suppressRules</c> switches a rule off for the project, and <c>x-atc-suppress</c> for one operation, path or schema.
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
        var diagnostics = Run(Marker, specificationOptions: []);

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
    public void TheSpecificationFilesSeverity_IsTheRulesSeverity(
        string severity,
        DiagnosticSeverity? expected)
    {
        var diagnostics = Run(Marker, specificationOptions: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"dotnet_diagnostic.{Generator.RuleIdentifiers.GetOperationIdPrefix}.severity"] = severity,
        });

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

    [Fact]
    public void TheMarkersSuppressRules_SwitchARuleOff()
    {
        var diagnostics = Run(
            """{ "validateSpecificationStrategy": "Strict", "suppressRules": ["ATC_API_OPR003"] }""",
            specificationOptions: []);

        Assert.DoesNotContain(diagnostics, d => d.Id == Generator.RuleIdentifiers.GetOperationIdPrefix);
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
            """{ "validateSpecificationStrategy": "Strict", "suppressRules": ["ATC_API_SCH013"] }""",
            specificationOptions: new Dictionary<string, string>(StringComparer.Ordinal) { ["dotnet_diagnostic.ATC_API_SCH013.severity"] = "none" },
            yaml);

        Assert.Contains(diagnostics, d => d.Id == "ATC_API_SCH013" && d.Severity == DiagnosticSeverity.Error);
    }

    private static List<Diagnostic> Run(
        string marker,
        Dictionary<string, string> specificationOptions,
        string yaml = Yaml)
    {
        var compilation = CSharpCompilation.Create(
            "Things",
            [],
            CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: false),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create([new ApiServerGenerator().AsSourceGenerator()], optionsProvider: new FileOptions(SpecPath, specificationOptions))
            .AddAdditionalTexts(
            [
                new CompilationVerificationHarness.InMemoryAdditionalText(SpecPath, yaml),
                new CompilationVerificationHarness.InMemoryAdditionalText("/project/.atc-rest-api-server", marker),
            ]);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, CancellationToken.None);
        return [.. diagnostics];
    }

    // What the build gives a generator for an additional file: the .editorconfig sections that match its path (and the
    // global configs); nothing for another file
    private sealed class FileOptions(
        string path,
        Dictionary<string, string> values) : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
    {
        private static readonly Options Empty = new([]);

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions
            => Empty;

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(
            SyntaxTree tree)
            => Empty;

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(
            AdditionalText textFile)
            => string.Equals(textFile.Path, path, StringComparison.Ordinal) ? new Options(values) : Empty;

        private sealed class Options(Dictionary<string, string> values) : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
        {
            public override bool TryGetValue(
                string key,
                [NotNullWhen(true)] out string? value)
                => values.TryGetValue(key, out value);
        }
    }
}