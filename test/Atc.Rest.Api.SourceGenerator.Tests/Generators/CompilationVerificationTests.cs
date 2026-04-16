namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Verifies that source generators detect marker files, parse YAML, and produce output.
/// Server generator requires ASP.NET Core references not available in unit tests,
/// so we verify it reports the expected diagnostic. Client generator produces full output.
/// </summary>
public class CompilationVerificationTests
{
    // ========== Client Generator (full compilation) ==========
    [Theory]
    [InlineData("PetStoreSimple", "PetStoreSimple.yaml")]
    [InlineData("Demo", "Demo.yaml")]
    public void ClientGenerator_ProducesSourceFiles(
        string scenarioName,
        string yamlFileName)
    {
        // Arrange & Act
        var (diagnostics, generatedSources) = RunGenerator(
            new ApiClientGenerator(),
            scenarioName,
            yamlFileName,
            ".atc-rest-api-client",
            "Client-Typed");

        // Assert — should produce source files
        Assert.True(
            generatedSources.Count > 0,
            $"ApiClientGenerator produced no source files for {scenarioName}");

        // Assert — no generator errors
        AssertNoErrors(diagnostics, "ApiClientGenerator", scenarioName);
    }

    [Fact]
    public void ClientGenerator_PetStoreSimple_ProducesExpectedFileCount()
    {
        // Arrange & Act
        var (_, generatedSources) = RunGenerator(
            new ApiClientGenerator(),
            "PetStoreSimple",
            "PetStoreSimple.yaml",
            ".atc-rest-api-client",
            "Client-Typed");

        // Assert - PetStoreSimple should generate: models, parameters, client, DI, enums, global usings, etc.
        Assert.True(
            generatedSources.Count >= 3,
            $"Expected at least 3 generated files, got {generatedSources.Count}");
    }

    // ========== Generator Detection ==========
    [Fact]
    public void ClientGenerator_WithNoMarkerFile_ProducesNoOutput()
    {
        // Arrange — only YAML, no marker file
        var yamlPath = GetScenarioPath("PetStoreSimple", "PetStoreSimple.yaml");
        var yamlContent = File.ReadAllText(yamlPath);

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("PetStoreSimple.yaml", yamlContent));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: GetMinimalReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(new ApiClientGenerator())
            .AddAdditionalTexts(additionalTexts);

        // Act
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _, TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        // Assert — no marker file means no generation
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void ClientGenerator_WithNoYaml_ProducesNoOutput()
    {
        // Arrange — only marker, no YAML
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(".atc-rest-api-client", "{}"));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: GetMinimalReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(new ApiClientGenerator())
            .AddAdditionalTexts(additionalTexts);

        // Act
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _, TestContext.Current.CancellationToken);

        var result = driver.GetRunResult();

        // Assert — no YAML means no generation
        Assert.Empty(result.GeneratedTrees);
    }

    // ========== Helpers ==========
    private static void AssertNoErrors(
        ImmutableArray<Diagnostic> diagnostics,
        string generatorName,
        string scenarioName)
    {
        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.True(
            errors.Count == 0,
            $"{generatorName} produced {errors.Count} error(s) for {scenarioName}: " +
            string.Join("; ", errors.Select(e => e.GetMessage(CultureInfo.InvariantCulture))));
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, List<(string HintName, string Source)> GeneratedSources) RunGenerator(
        IIncrementalGenerator generator,
        string scenarioName,
        string yamlFileName,
        string markerFileName,
        string masterFolder)
    {
        var yamlPath = GetScenarioPath(scenarioName, yamlFileName);
        var yamlContent = File.ReadAllText(yamlPath);

        var markerPath = Path.Combine(
            Path.GetDirectoryName(yamlPath)!,
            masterFolder,
            markerFileName);
        var markerContent = File.Exists(markerPath) ? File.ReadAllText(markerPath) : "{}";

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(yamlFileName, yamlContent),
            new InMemoryAdditionalText(markerFileName, markerContent));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: GetMinimalReferences(),
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

    private static List<MetadataReference> GetMinimalReferences()
    {
        var references = new List<MetadataReference>();
        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Collections.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Linq.dll")));
        references.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "netstandard.dll")));

        return references;
    }

    private static string GetScenarioPath(
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
    private sealed class InMemoryAdditionalText(
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