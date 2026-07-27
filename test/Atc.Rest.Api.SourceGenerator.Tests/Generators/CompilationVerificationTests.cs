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
        var (diagnostics, generatedSources) = CompilationVerificationHarness.RunGenerator(
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
        var (_, generatedSources) = CompilationVerificationHarness.RunGenerator(
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

    // ========== Client Generator (real C# compilation of generated output) ==========
    [Theory]
    [InlineData("PetStoreSimple", "PetStoreSimple.yaml")]
    [InlineData("Demo", "Demo.yaml")]
    [InlineData("HttpMethods", "HttpMethods.yaml")]
    [InlineData("StreamingItemSchema", "StreamingItemSchema.yaml")]
    [InlineData("ParameterSerialization", "ParameterSerialization.yaml")]
    [InlineData("ComponentsReuse", "ComponentsReuse.yaml")]
    public void ClientGenerator_GeneratedCode_CompilesWithoutErrors(
        string scenarioName,
        string yamlFileName)
    {
        // Arrange & Act — generate the typed client for the scenario.
        var (_, generatedSources) = CompilationVerificationHarness.RunGenerator(
            new ApiClientGenerator(),
            scenarioName,
            yamlFileName,
            ".atc-rest-api-client",
            "Client-Typed");

        Assert.NotEmpty(generatedSources);

        // Assert — the generated client compiles with no errors.
        var errors = CompilationVerificationHarness.CompileGeneratedSources(generatedSources);

        Assert.True(
            errors.Count == 0,
            $"Generated client for {scenarioName} did not compile:\n" +
            string.Join("\n", errors));
    }

    // ========== Per-Operation Client Generator (real C# compilation of generated output) ==========
    [Theory]
    [InlineData("StreamingItemSchema", "StreamingItemSchema.yaml")]
    [InlineData("ParameterSerialization", "ParameterSerialization.yaml")]
    [InlineData("ComponentsReuse", "ComponentsReuse.yaml")]
    public void ClientGenerator_PerOperation_GeneratedCode_CompilesWithoutErrors(
        string scenarioName,
        string yamlFileName)
    {
        // Arrange & Act — generate the per-operation client (Client-Operation marker selects
        // EndpointPerOperation mode), which emits endpoint/interface/result classes per segment.
        // EndpointPerOperation mode is gated on the Atc.Rest.Client reference being present,
        // so supply the full reference set for the generator to emit output.
        var (_, generatedSources) = CompilationVerificationHarness.RunGenerator(
            new ApiClientGenerator(),
            scenarioName,
            yamlFileName,
            ".atc-rest-api-client",
            "Client-Operation",
            useFullReferences: true);

        Assert.NotEmpty(generatedSources);

        // Assert — the generated per-operation client compiles with no errors.
        var errors = CompilationVerificationHarness.CompileGeneratedSources(generatedSources);

        Assert.True(
            errors.Count == 0,
            $"Generated per-operation client for {scenarioName} did not compile:\n" +
            string.Join("\n", errors));
    }

    [Theory]
    [InlineData("PetStoreSimple", "PetStoreSimple.yaml")]
    [InlineData("Demo", "Demo.yaml")]
    [InlineData("HttpMethods", "HttpMethods.yaml")]
    [InlineData("StreamingItemSchema", "StreamingItemSchema.yaml")]
    [InlineData("ParameterSerialization", "ParameterSerialization.yaml")]
    [InlineData("ComponentsReuse", "ComponentsReuse.yaml")]
    [InlineData("RateLimit", "RateLimit.yaml")]
    [InlineData("CachingOutput", "CachingOutput.yaml")]
    public void ServerGenerator_GeneratedCode_CompilesWithoutErrors(
        string scenarioName,
        string yamlFileName)
    {
        // Arrange & Act — generate the server for the scenario.
        var (_, generatedSources) = CompilationVerificationHarness.RunGenerator(
            new ApiServerGenerator(),
            scenarioName,
            yamlFileName,
            ".atc-rest-api-server",
            "Server",
            useFullReferences: true);

        Assert.NotEmpty(generatedSources);

        // Assert — the generated server compiles with no errors.
        var errors = CompilationVerificationHarness.CompileGeneratedSources(generatedSources);

        Assert.True(
            errors.Count == 0,
            $"Generated server for {scenarioName} did not compile:\n" +
            string.Join("\n", errors));
    }

    // ========== GeneratedCode attribute coverage ==========
    // These files are built via ApiClientGenerator's own StringBuilder-based emission (not the
    // shared CodeGenerationService extractors), so they don't show up in the CodeGenerationService
    // snapshot suite. Assert directly that every top-level type carries [GeneratedCode].
    [Fact]
    public void ClientGenerator_PerOperation_SharedTypes_ContainGeneratedCodeAttribute()
    {
        // Arrange & Act
        var (_, generatedSources) = CompilationVerificationHarness.RunGenerator(
            new ApiClientGenerator(),
            "PetStoreSimple",
            "PetStoreSimple.yaml",
            ".atc-rest-api-client",
            "Client-Operation",
            useFullReferences: true);

        string? SourceContaining(string marker)
            => generatedSources
                .Select(s => s.Source)
                .FirstOrDefault(s => s.Contains(marker, StringComparison.Ordinal));

        // Assert
        var problemDetails = SourceContaining("public record ProblemDetails(");
        Assert.NotNull(problemDetails);
        Assert.Contains($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]", problemDetails, StringComparison.Ordinal);

        var validationProblemDetails = SourceContaining("public record ValidationProblemDetails(");
        Assert.NotNull(validationProblemDetails);
        Assert.Contains($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]", validationProblemDetails, StringComparison.Ordinal);

        var problemDetailsFactory = SourceContaining("public static class ProblemDetailsFactory");
        Assert.NotNull(problemDetailsFactory);
        Assert.Contains($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]", problemDetailsFactory, StringComparison.Ordinal);

        var constants = SourceContaining("public static class Constants");
        Assert.NotNull(constants);
        Assert.Contains($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]", constants, StringComparison.Ordinal);

        var segmentDiExtension = SourceContaining("public static class EndpointsServiceCollectionExtensions");
        Assert.NotNull(segmentDiExtension);
        Assert.Contains($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]", segmentDiExtension, StringComparison.Ordinal);

        var consolidatedDiExtension = SourceContaining("public static class PetStoreSimpleEndpointsServiceCollectionExtensions");
        Assert.NotNull(consolidatedDiExtension);
        Assert.Contains($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]", consolidatedDiExtension, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".atc-rest-api-server", "Server")]
    public void ServerGenerator_ExcludeFromCodeCoverageTrue_AddsAttributeAndCompiles(
        string markerFileName,
        string masterFolder)
        => AssertExcludeFromCodeCoverageAppliedAndCompiles(new ApiServerGenerator(), markerFileName, masterFolder);

    [Theory]
    [InlineData(".atc-rest-api-server", "Server")]
    public void ServerGenerator_ExcludeFromCodeCoverageDefault_DoesNotAddAttribute(
        string markerFileName,
        string masterFolder)
        => AssertExcludeFromCodeCoverageDefaultOff(new ApiServerGenerator(), markerFileName, masterFolder);

    [Theory]
    [InlineData(".atc-rest-api-client", "Client-Typed")]
    public void ClientGenerator_Typed_ExcludeFromCodeCoverageTrue_AddsAttributeAndCompiles(
        string markerFileName,
        string masterFolder)
        => AssertExcludeFromCodeCoverageAppliedAndCompiles(new ApiClientGenerator(), markerFileName, masterFolder);

    [Theory]
    [InlineData(".atc-rest-api-client", "Client-Typed")]
    public void ClientGenerator_Typed_ExcludeFromCodeCoverageDefault_DoesNotAddAttribute(
        string markerFileName,
        string masterFolder)
        => AssertExcludeFromCodeCoverageDefaultOff(new ApiClientGenerator(), markerFileName, masterFolder);

    [Theory]
    [InlineData(".atc-rest-api-client", "Client-Operation")]
    public void ClientGenerator_PerOperation_ExcludeFromCodeCoverageTrue_AddsAttributeAndCompiles(
        string markerFileName,
        string masterFolder)
        => AssertExcludeFromCodeCoverageAppliedAndCompiles(new ApiClientGenerator(), markerFileName, masterFolder);

    [Theory]
    [InlineData(".atc-rest-api-client", "Client-Operation")]
    public void ClientGenerator_PerOperation_ExcludeFromCodeCoverageDefault_DoesNotAddAttribute(
        string markerFileName,
        string masterFolder)
        => AssertExcludeFromCodeCoverageDefaultOff(new ApiClientGenerator(), markerFileName, masterFolder);

    // ========== Generator Detection ==========
    [Fact]
    public void ClientGenerator_WithNoMarkerFile_ProducesNoOutput()
    {
        // Arrange — only YAML, no marker file
        var yamlPath = CompilationVerificationHarness.GetScenarioPath("PetStoreSimple", "PetStoreSimple.yaml");
        var yamlContent = File.ReadAllText(yamlPath);

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new CompilationVerificationHarness.InMemoryAdditionalText("PetStoreSimple.yaml", yamlContent));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: CompilationVerificationHarness.GetMinimalReferences(),
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
            new CompilationVerificationHarness.InMemoryAdditionalText(".atc-rest-api-client", "{}"));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: CompilationVerificationHarness.GetMinimalReferences(),
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
    private static void AssertExcludeFromCodeCoverageAppliedAndCompiles(
        IIncrementalGenerator generator,
        string markerFileName,
        string masterFolder)
    {
        // Arrange
        var yamlPath = CompilationVerificationHarness.GetScenarioPath("PetStoreSimple", "PetStoreSimple.yaml");
        var yamlContent = File.ReadAllText(yamlPath);

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new CompilationVerificationHarness.InMemoryAdditionalText("PetStoreSimple.yaml", yamlContent),
            new CompilationVerificationHarness.InMemoryAdditionalText(markerFileName, """{"excludeFromCodeCoverage": true}"""));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: CompilationVerificationHarness.GetFullFrameworkReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts);

        // Act
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _, TestContext.Current.CancellationToken);

        var generatedSources = driver.GetRunResult().GeneratedTrees
            .Select(t => (HintName: Path.GetFileName(t.FilePath), Source: t.GetText().ToString()))
            .ToList();

        // Assert — every class/struct/record [GeneratedCode] also carries [ExcludeFromCodeCoverage].
        // Interfaces/enums/delegates are excluded ([ExcludeFromCodeCoverage] isn't a valid target
        // there), so this is expected to be less than or equal to the total [GeneratedCode] count.
        var generatedCodeCount = generatedSources.Sum(s => s.Source.Split("[GeneratedCode(").Length - 1);
        var excludeCount = generatedSources.Sum(s => s.Source.Split("[ExcludeFromCodeCoverage]").Length - 1);

        Assert.True(generatedCodeCount > 0, $"Expected at least one [GeneratedCode] attribute for {masterFolder}.");
        Assert.True(excludeCount > 0, $"Expected at least one [ExcludeFromCodeCoverage] attribute for {masterFolder}.");
        Assert.True(
            excludeCount <= generatedCodeCount,
            $"[ExcludeFromCodeCoverage] count ({excludeCount}) exceeded [GeneratedCode] count ({generatedCodeCount}) for {masterFolder}.");

        // Assert — the generated output still compiles (proves the using directive was inserted correctly).
        var errors = CompilationVerificationHarness.CompileGeneratedSources(
            generatedSources.Select(s => (s.HintName, s.Source)).ToList());

        Assert.True(
            errors.Count == 0,
            $"Generated {masterFolder} output with excludeFromCodeCoverage did not compile:\n" + string.Join("\n", errors));
    }

    private static void AssertExcludeFromCodeCoverageDefaultOff(
        IIncrementalGenerator generator,
        string markerFileName,
        string masterFolder)
    {
        // Arrange & Act — default marker config (no excludeFromCodeCoverage key).
        var (_, generatedSources) = CompilationVerificationHarness.RunGenerator(
            generator,
            "PetStoreSimple",
            "PetStoreSimple.yaml",
            markerFileName,
            masterFolder,
            useFullReferences: true);

        // Assert
        Assert.DoesNotContain(generatedSources, s => s.Source.Contains("[ExcludeFromCodeCoverage]", StringComparison.Ordinal));
    }

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
}