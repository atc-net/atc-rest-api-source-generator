namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Verifies that <see cref="ApiServerDomainGenerator"/> honors the <c>excludeFromCodeCoverage</c>
/// marker config for its DI registration output. Runs alongside <see cref="ApiServerGenerator"/>
/// in one compilation (as a real Host+Contracts project pairing would) so the handler interfaces
/// and result/parameter types the Domain output references actually resolve, letting the whole
/// thing compile for real. Uses a temporary marker directory (the generator writes physical
/// handler scaffold files next to the marker) to avoid touching the checked-in scenario folders.
/// </summary>
public sealed class ApiServerDomainGeneratorExcludeFromCodeCoverageTests : IDisposable
{
    private readonly string tempMarkerDirectory = Path.Combine(Path.GetTempPath(), "atc-domain-gen-tests-" + Guid.NewGuid().ToString("N"));

    public ApiServerDomainGeneratorExcludeFromCodeCoverageTests()
        => Directory.CreateDirectory(tempMarkerDirectory);

    public void Dispose()
    {
        if (Directory.Exists(tempMarkerDirectory))
        {
            Directory.Delete(tempMarkerDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ExcludeFromCodeCoverageTrue_DependencyRegistrationCarriesAttributeAndCompiles()
    {
        var generatedSources = RunGenerators(excludeFromCodeCoverage: true);

        var diRegistration = generatedSources
            .Select(s => s.Source)
            .FirstOrDefault(s => s.Contains("AddApiHandlersFrom", StringComparison.Ordinal));

        Assert.NotNull(diRegistration);
        Assert.Contains("using System.Diagnostics.CodeAnalysis;", diRegistration, StringComparison.Ordinal);
        Assert.Contains("[ExcludeFromCodeCoverage]", diRegistration, StringComparison.Ordinal);

        var errors = CompilationVerificationHarness.CompileGeneratedSources(
            generatedSources.Select(s => (s.HintName, s.Source)).ToList());

        Assert.True(
            errors.Count == 0,
            "Generated Server+ServerDomain output with excludeFromCodeCoverage did not compile:\n" + string.Join("\n", errors));
    }

    [Fact]
    public void ExcludeFromCodeCoverageDefault_DoesNotAddAttribute()
    {
        var generatedSources = RunGenerators(excludeFromCodeCoverage: false);

        Assert.DoesNotContain(generatedSources, s => s.Source.Contains("[ExcludeFromCodeCoverage]", StringComparison.Ordinal));
    }

    private List<(string HintName, string Source)> RunGenerators(
        bool excludeFromCodeCoverage)
    {
        var yamlPath = CompilationVerificationHarness.GetScenarioPath("PetStoreSimple", "PetStoreSimple.yaml");
        var yamlContent = File.ReadAllText(yamlPath);

        var handlersMarkerPath = Path.Combine(tempMarkerDirectory, ".atc-rest-api-server-handlers");
        var handlersMarkerContent = excludeFromCodeCoverage
            ? """{"excludeFromCodeCoverage": true}"""
            : "{}";

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new CompilationVerificationHarness.InMemoryAdditionalText("PetStoreSimple.yaml", yamlContent),
            new CompilationVerificationHarness.InMemoryAdditionalText(".atc-rest-api-server", "{}"),
            new CompilationVerificationHarness.InMemoryAdditionalText(handlersMarkerPath, handlersMarkerContent));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: CompilationVerificationHarness.GetFullFrameworkReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(new ApiServerGenerator(), new ApiServerDomainGenerator())
            .AddAdditionalTexts(additionalTexts);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _, TestContext.Current.CancellationToken);

        return driver.GetRunResult().GeneratedTrees
            .Select(t => (HintName: Path.GetFileName(t.FilePath), Source: t.GetText().ToString()))
            .ToList();
    }
}