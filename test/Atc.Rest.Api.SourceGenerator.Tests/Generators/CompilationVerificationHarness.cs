namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Shared compilation/emit infrastructure for the source-generator tests. Runs a generator
/// over a scenario's YAML + marker file, compiles the generated output as a real assembly, and
/// (optionally) loads it so tests can invoke the emitted types over real wire bytes.
/// </summary>
internal static class CompilationVerificationHarness
{
    /// <summary>
    /// Runs the typed C# client generator for a scenario and returns the generated sources.
    /// </summary>
    public static List<(string HintName, string Source)> RunClient(
        string scenarioName,
        string yamlFileName)
        => RunGenerator(
            new ApiClientGenerator(),
            scenarioName,
            yamlFileName,
            ".atc-rest-api-client",
            "Client-Typed").GeneratedSources;

    /// <summary>
    /// Runs the server generator for a scenario (with full references) and returns the generated sources.
    /// </summary>
    public static List<(string HintName, string Source)> RunServer(
        string scenarioName,
        string yamlFileName)
        => RunGenerator(
            new ApiServerGenerator(),
            scenarioName,
            yamlFileName,
            ".atc-rest-api-server",
            "Server",
            useFullReferences: true).GeneratedSources;

    /// <summary>
    /// Runs a generator over a scenario and returns both the generator diagnostics and the
    /// generated sources (hint name + source text).
    /// </summary>
    public static (ImmutableArray<Diagnostic> Diagnostics, List<(string HintName, string Source)> GeneratedSources) RunGenerator(
        IIncrementalGenerator generator,
        string scenarioName,
        string yamlFileName,
        string markerFileName,
        string masterFolder,
        bool useFullReferences = false)
    {
        var yamlPath = GetScenarioPath(scenarioName, yamlFileName);
        var yamlContent = File.ReadAllText(yamlPath);

        var markerPath = Path.Combine(
            Path.GetDirectoryName(yamlPath),
            masterFolder,
            markerFileName);
        var markerContent = File.Exists(markerPath) ? File.ReadAllText(markerPath) : "{}";

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(yamlFileName, yamlContent),
            new InMemoryAdditionalText(markerFileName, markerContent));

        // The server generator gates on ASP.NET Core references being present; supply the
        // full reference set when the caller needs the generator to actually emit output.
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: useFullReferences ? GetFullFrameworkReferences() : GetMinimalReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out var generatorDiagnostics, CancellationToken.None);

        var result = driver.GetRunResult();
        var generatedSources = result.GeneratedTrees
            .Select(t => (t.FilePath, t.GetText().ToString()))
            .ToList();

        return (generatorDiagnostics, generatedSources);
    }

    /// <summary>
    /// Compiles the generated sources as a real C# assembly and returns any compile errors.
    /// Supplies the host project's ImplicitUsings (which generated code assumes) and the full
    /// framework + ASP.NET Core + Atc.Rest.Client reference set.
    /// </summary>
    public static List<string> CompileGeneratedSources(
        List<(string HintName, string Source)> generatedSources)
    {
        var compilation = CreateCompilation(generatedSources);

        return compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToList();
    }

    /// <summary>
    /// Compiles the generated sources to an in-memory assembly and loads it, asserting the
    /// emit succeeded. Used by wire-byte round-trip tests that invoke emitted types via reflection.
    /// </summary>
    public static Assembly EmitAndLoad(
        List<(string HintName, string Source)> generatedSources)
    {
        var compilation = CreateCompilation(generatedSources);

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(
            emitResult.Success,
            "Emit failed:\n" + string.Join(
                "\n",
                emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString())));

        return Assembly.Load(ms.ToArray());
    }

    private static CSharpCompilation CreateCompilation(
        List<(string HintName, string Source)> generatedSources)
    {
        // Generated code assumes the host project's ImplicitUsings; supply the standard set
        // so BCL types (Task, IAsyncEnumerable, HttpClient, ...) resolve without per-file usings.
        const string implicitUsings = """
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Net.Http;
            global using System.Threading;
            global using System.Threading.Tasks;
            """;

        var trees = generatedSources
            .Select(s => CSharpSyntaxTree.ParseText(
                SourceText.From(s.Source, Encoding.UTF8),
                cancellationToken: TestContext.Current.CancellationToken))
            .Append(CSharpSyntaxTree.ParseText(
                SourceText.From(implicitUsings, Encoding.UTF8),
                cancellationToken: TestContext.Current.CancellationToken))
            .ToList();

        return CSharpCompilation.Create(
            "GeneratedCodeCompileTest",
            trees,
            GetFullFrameworkReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    public static List<MetadataReference> GetMinimalReferences()
    {
        var references = new List<MetadataReference>();
        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);

        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Collections.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Linq.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "netstandard.dll")));

        return references;
    }

    /// <summary>
    /// Full reference set: every assembly the test host can see (the trusted-platform-assemblies
    /// list), which includes the BCL, the ASP.NET Core shared framework, and Atc.Rest.Client —
    /// so generated code compiles for real.
    /// </summary>
    public static List<MetadataReference> GetFullFrameworkReferences()
    {
        var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        return tpa
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
    }

    public static string GetScenarioPath(
        string scenarioName,
        string fileName)
    {
        var testDir = AppContext.BaseDirectory;
        return Path.GetFullPath(
            Path.Combine(testDir, "..", "..", "..", "..", "Scenarios", scenarioName, fileName));
    }

    /// <summary>
    /// In-memory AdditionalText for generator driver tests.
    /// </summary>
    public sealed class InMemoryAdditionalText(
        string path,
        string content) : AdditionalText
    {
        private readonly SourceText sourceText = SourceText.From(content, Encoding.UTF8);

        public override string Path { get; } = path;

        public override SourceText GetText(
            CancellationToken cancellationToken = default)
            => sourceText;
    }
}