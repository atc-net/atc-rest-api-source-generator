namespace Atc.Rest.Api.Generator.Benchmarks;

/// <summary>
/// Generation time and allocations of each generator for a large specification: a cold run (a new driver, as on the
/// first build) and an incremental run (the same driver after an unrelated C# edit, as on a keystroke in the IDE).
/// </summary>
[MemoryDiagnoser]
public class GeneratorBenchmarks
{
    private const string UnrelatedSource = "namespace Contoso.Host { public static class Unrelated { public static int Value => 1; } }";

    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    private CSharpCompilation compilation = null!;
    private ImmutableArray<AdditionalText> additionalTexts;
    private GeneratorDriver warmDriver = null!;
    private CSharpCompilation editedCompilation = null!;
    private string scaffoldFolder = null!;
    private string specificationText = null!;
    private int edits;

    /// <summary>The specification (a sample under <c>sample/</c>).</summary>
    [Params("Showcase", "PetStoreFull")]
    public string Specification { get; set; } = string.Empty;

    /// <summary>The generator and its marker file.</summary>
    [Params("Server", "Client", "Domain")]
    public string Generator { get; set; } = string.Empty;

    /// <summary>Builds the compilation, the additional texts and a driver that has already run once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var specPath = Path.Combine(AppContext.BaseDirectory, "Specs", $"{Specification}.yaml");
        specificationText = File.ReadAllText(specPath);

        // The domain generator writes its handler scaffolds next to the marker: keep them in a temp folder
        scaffoldFolder = Path.Combine(Path.GetTempPath(), "atc-rest-api-benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scaffoldFolder);

        var (markerName, markerContent) = Generator switch
        {
            "Server" => (".atc-rest-api-server", "{ \"validateSpecificationStrategy\": \"Standard\" }"),
            "Client" => (".atc-rest-api-client", "{ \"validateSpecificationStrategy\": \"Standard\" }"),
            _ => (".atc-rest-api-server-handlers", "{ \"generate\": true, \"validateSpecificationStrategy\": \"Standard\" }"),
        };

        additionalTexts =
        [
            new InMemoryAdditionalText(Path.Combine(scaffoldFolder, $"{Specification}.yaml"), specificationText),
            new InMemoryAdditionalText(Path.Combine(scaffoldFolder, markerName), markerContent),
        ];

        compilation = CSharpCompilation.Create(
            $"{Specification}.{Generator}",
            [CSharpSyntaxTree.ParseText(UnrelatedSource, ParseOptions), .. ProjectBody()],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        warmDriver = CreateDriver().RunGenerators(compilation);

        // A benchmark of a generator that generated nothing measures nothing
        var result = warmDriver.GetRunResult();
        if (result.GeneratedTrees.Length < 5)
        {
            var diagnostics = string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException($"{Generator} generated {result.GeneratedTrees.Length} sources for {Specification}:{Environment.NewLine}{diagnostics}");
        }

        // An edit that touches no input of the generator: whitespace in an unrelated file
        var tree = compilation.SyntaxTrees[0];
        editedCompilation = compilation.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(UnrelatedSource + " ", ParseOptions));
    }

    /// <summary>Removes the scaffolds the domain generator wrote.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(scaffoldFolder))
        {
            Directory.Delete(scaffoldFolder, recursive: true);
        }
    }

    /// <summary>A first build: a new driver runs the generator from scratch.</summary>
    [Benchmark(Baseline = true)]
    public GeneratorDriver Cold()
        => CreateDriver().RunGenerators(compilation);

    /// <summary>A keystroke in an unrelated C# file: the warm driver runs again.</summary>
    [Benchmark]
    public GeneratorDriver IncrementalUnrelatedEdit()
        => warmDriver.RunGenerators(editedCompilation);

    /// <summary>An edit in the specification: the warm driver runs again on a text it has not parsed before.</summary>
    [Benchmark]
    public GeneratorDriver IncrementalSpecificationEdit()
    {
        // A new comment each time, so the parse cache (keyed by the content) cannot answer
        var edited = new InMemoryAdditionalText(additionalTexts[0].Path, specificationText + Environment.NewLine + "# edit " + (++edits) + Environment.NewLine);
        return warmDriver
            .ReplaceAdditionalText(additionalTexts[0], edited)
            .RunGenerators(compilation);
    }

    private GeneratorDriver CreateDriver()
    {
        IIncrementalGenerator generator = Generator switch
        {
            "Server" => new ApiServerGenerator(),
            "Client" => new ApiClientGenerator(),
            _ => new ApiServerDomainGenerator(),
        };

        return CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            additionalTexts,
            ParseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: false));
    }

    /// <summary>
    /// A project of realistic size: 400 classes with a base list over 20 namespaces, every tenth one a handler -
    /// what the domain generator looks through on each edit.
    /// </summary>
    private static IEnumerable<SyntaxTree> ProjectBody()
        => Enumerable.Range(0, 400).Select(i => CSharpSyntaxTree.ParseText(
            i % 10 == 0
                ? $"namespace Contoso.Domain.Area{i % 20} {{ public sealed class Thing{i}Handler {{ }} }}"
                : $"namespace Contoso.Domain.Area{i % 20} {{ public sealed class Service{i} : System.IDisposable {{ public void Dispose() {{ }} }} }}",
            ParseOptions));

    private static List<MetadataReference> References()
    {
        var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? [];
        return [.. trusted.Select(path => MetadataReference.CreateFromFile(path))];
    }

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        private readonly SourceText text = SourceText.From(content);

        public override string Path { get; } = path;

        public override SourceText GetText(
            CancellationToken cancellationToken = default)
            => text;
    }
}