namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The incremental behaviour of the three generators, observed with Roslyn's step tracking: an edit to C# code
/// that does not concern a generator leaves its output cached, an edit to the marker or the specification runs
/// it again, and a path filter taken out of the marker brings the paths back (the parse cache hands out a
/// document the filter must not mutate).
/// </summary>
public class IncrementalCachingTests
{
    private const string ProjectName = "Contoso.Api";

    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Contoso
          version: 1.0.0
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
                        type: array
                        items:
                          $ref: '#/components/schemas/Thing'
          /others:
            get:
              operationId: listOthers
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Thing'
        components:
          schemas:
            Thing:
              type: object
              required: [name]
              properties:
                name: { type: string }
        """;

    public static IEnumerable<object[]> Generators
        =>
        [
            ["server", ".atc-rest-api-server"],
            ["domain", ".atc-rest-api-server-handlers"],
            ["client", ".atc-rest-api-client"],
        ];

    /// <summary>
    /// A C# edit that changes nothing a generator reads - a new class in the project - leaves the generator's
    /// output step cached: nothing is generated again. The step that reads the compilation runs (it must look),
    /// but produces the same value.
    /// </summary>
    [Theory]
    [MemberData(nameof(Generators))]
    public void UnrelatedSyntaxEdit_LeavesTheOutputCached(
        string generator,
        string markerFileName)
    {
        var run = new TrackedRun(Create(generator), [Spec(Yaml), (markerFileName, "{}")]);
        run.Run();

        run.AddSource("namespace Contoso.Unrelated { public class Nothing { } }");
        run.Run();

        var reasons = run.OutputStepReasons();
        Assert.NotEmpty(reasons);
        Assert.All(reasons, r => Assert.Equal(IncrementalStepRunReason.Cached, r.Reason));
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void MarkerEdit_RunsTheOutputAgain(
        string generator,
        string markerFileName)
    {
        var run = new TrackedRun(Create(generator), [Spec(Yaml), (markerFileName, "{}")]);
        run.Run();

        run.Replace(markerFileName, """{ "includeDeprecated": true }""");
        run.Run();

        var reasons = run.OutputStepReasons();
        Assert.NotEmpty(reasons);
        Assert.All(reasons, r => Assert.Equal(IncrementalStepRunReason.Modified, r.Reason));
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void SpecificationEdit_RunsTheOutputAgain(
        string generator,
        string markerFileName)
    {
        var run = new TrackedRun(Create(generator), [Spec(Yaml), (markerFileName, "{}")]);
        run.Run();

        run.Replace(ProjectName + ".yaml", Yaml.Replace("title: Contoso", "title: Contoso v2", StringComparison.Ordinal));
        run.Run();

        var reasons = run.OutputStepReasons();
        Assert.NotEmpty(reasons);
        Assert.All(reasons, r => Assert.Equal(IncrementalStepRunReason.Modified, r.Reason));
    }

    /// <summary>
    /// An edit to an overlay the marker lists - and to nothing else - generates again, with the overlay's change.
    /// </summary>
    [Theory]
    [MemberData(nameof(Generators))]
    public void OverlayEdit_RunsTheOutputAgain(
        string generator,
        string markerFileName)
    {
        const string overlay = """
            overlay: 1.2.0
            info: { title: t, version: 1.0.0 }
            actions:
              - target: $.info
                update: { description: first }
            """;
        var run = new TrackedRun(Create(generator), [Spec(Yaml), (markerFileName, """{ "overlays": ["Contoso.overlay.yaml"] }"""), ("Contoso.overlay.yaml", overlay)]);
        run.Run();

        run.Replace("Contoso.overlay.yaml", overlay.Replace("description: first", "description: second", StringComparison.Ordinal));
        run.Run();

        var reasons = run.OutputStepReasons();
        Assert.NotEmpty(reasons);
        Assert.All(reasons, r => Assert.Equal(IncrementalStepRunReason.Modified, r.Reason));
    }

    /// <summary>
    /// The second run of the same inputs generates the same sources: nothing in the pipeline depends on the run.
    /// </summary>
    [Theory]
    [MemberData(nameof(Generators))]
    public void SameInputs_GenerateTheSameSources(
        string generator,
        string markerFileName)
    {
        var run = new TrackedRun(Create(generator), [Spec(Yaml), (markerFileName, "{}")]);
        run.Run();
        var first = run.GeneratedSources();
        Assert.NotEmpty(first);

        run.AddSource("namespace Contoso.Unrelated { public class Nothing { } }");
        run.Run();

        Assert.Equal(first, run.GeneratedSources());
    }

    /// <summary>
    /// A path filter in the marker leaves out the other paths; taking the filter out of the marker brings them
    /// back in the same compiler process - the filter must not have mutated the cached document.
    /// </summary>
    [Theory]
    [InlineData("server")]
    [InlineData("client")]
    [InlineData("domain")]
    public void IncludePathsRemovedFromTheMarker_RestoresThePaths(
        string generator)
    {
        var markerFileName = generator switch { "server" => ".atc-rest-api-server", "client" => ".atc-rest-api-client", _ => ".atc-rest-api-server-handlers" };
        var run = new TrackedRun(Create(generator), [Spec(Yaml), (markerFileName, """{ "includePaths": ["/things"] }""")]);
        run.Run();
        var filtered = string.Join("\n", run.GeneratedSources().Select(s => s.Source));
        Assert.Contains("ListThings", filtered, StringComparison.Ordinal);
        Assert.DoesNotContain("ListOthers", filtered, StringComparison.Ordinal);

        run.Replace(markerFileName, "{}");
        run.Run();
        var unfiltered = string.Join("\n", run.GeneratedSources().Select(s => s.Source));
        Assert.Contains("ListThings", unfiltered, StringComparison.Ordinal);
        Assert.Contains("ListOthers", unfiltered, StringComparison.Ordinal);
    }

    private static IIncrementalGenerator Create(string generator)
        => generator switch
        {
            "server" => new ApiServerGenerator(),
            "domain" => new ApiServerDomainGenerator(),
            "client" => new ApiClientGenerator(),
            _ => throw new ArgumentOutOfRangeException(nameof(generator)),
        };

    private static (string Path, string Content) Spec(string yaml)
        => (ProjectName + ".yaml", yaml);

    /// <summary>
    /// A generator driver with step tracking on, whose additional texts and sources can be edited between runs.
    /// </summary>
    private sealed class TrackedRun
    {
        private readonly Dictionary<string, AdditionalText> texts;
        private readonly List<SyntaxTree> trees = [];
        private GeneratorDriver driver;
        private GeneratorDriverRunResult? result;

        public TrackedRun(
            IIncrementalGenerator generator,
            (string Path, string Content)[] additionalTexts)
        {
            texts = additionalTexts.ToDictionary(t => t.Path, t => (AdditionalText)new CompilationVerificationHarness.InMemoryAdditionalText(t.Path, t.Content), StringComparer.Ordinal);
            driver = CSharpGeneratorDriver.Create(
                [generator.AsSourceGenerator()],
                additionalTexts: texts.Values,
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        }

        public void AddSource(string source)
            => trees.Add(CSharpSyntaxTree.ParseText(source));

        public void Replace(
            string path,
            string content)
        {
            var replacement = new CompilationVerificationHarness.InMemoryAdditionalText(path, content);
            driver = driver.ReplaceAdditionalText(texts[path], replacement);
            texts[path] = replacement;
        }

        public void Run()
        {
            var compilation = CSharpCompilation.Create(
                ProjectName,
                trees,
                CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: true),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            driver = driver.RunGenerators(compilation, CancellationToken.None);
            result = driver.GetRunResult();
        }

        public List<(string Name, IncrementalStepRunReason Reason)> OutputStepReasons()
            => result!.Results[0].TrackedOutputSteps
                .SelectMany(step => step.Value.SelectMany(run => run.Outputs.Select(o => (step.Key, o.Reason))))
                .ToList();

        public List<(string HintName, string Source)> GeneratedSources()
            => result!.Results[0].GeneratedSources.Select(s => (s.HintName, s.SourceText.ToString())).OrderBy(s => s.HintName, StringComparer.Ordinal).ToList();
    }
}