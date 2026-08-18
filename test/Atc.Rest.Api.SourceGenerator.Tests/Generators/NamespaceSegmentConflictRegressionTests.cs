namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Regression tests for the case where a schema name collides with a segment of the root
/// namespace (e.g. schema "Device" inside "Contoso.Data.Device.Management.Api.Contracts").
/// <see cref="Atc.OpenApi.Models.TypeConflictRegistry"/> then emits the type fully qualified,
/// and the qualification must match the namespace the models are actually generated into.
/// Previously this produced either "...Generated..Models.Device" (empty segment) or
/// "...Generated.Devices.Models.Device" (raw, non-effective segment) - both uncompilable.
/// </summary>
public class NamespaceSegmentConflictRegressionTests
{
    private const string DeviceYaml =
        """
        openapi: 3.0.0
        info:
          title: Device Management API
          version: 1.0.0
        paths:
          /devices:
            get:
              operationId: getDevices
              responses:
                '200':
                  description: ok
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Device'
        components:
          schemas:
            Device:
              type: object
              properties:
                id:
                  type: string
        """;

    [Fact]
    public void ServerGenerator_SchemaNameCollidesWithRootNamespaceSegment_QualifiesToExistingNamespace()
    {
        // Arrange & Act
        var generatedSources = RunGenerator(
            new ApiServerGenerator(),
            "Contoso.Data.Device.Management.Api.Contracts",
            ".atc-rest-api-server",
            useFullReferences: true);

        // Assert
        AssertNoBrokenModelQualification(generatedSources);
    }

    [Fact]
    public void ClientGenerator_SchemaNameCollidesWithRootNamespaceSegment_QualifiesToExistingNamespace()
    {
        // Arrange & Act
        var generatedSources = RunGenerator(
            new ApiClientGenerator(),
            "Contoso.Data.Device.Management.Api.Client",
            ".atc-rest-api-client");

        // Assert
        AssertNoBrokenModelQualification(generatedSources);
    }

    private static void AssertNoBrokenModelQualification(
        List<(string HintName, string Source)> generatedSources)
    {
        Assert.NotEmpty(generatedSources);

        // The Device model lands directly under "{root}.Generated.Models" because the single
        // "devices" path segment is redundant, so every fully qualified reference to it must use
        // exactly that namespace - never an empty segment, never the raw "Devices" segment.
        var declaredNamespace = generatedSources
            .Select(s => Regex.Match(s.Source, @"namespace\s+(?<ns>[\w.]+\.Generated(?:\.[\w.]+)?\.Models)\s*;", RegexOptions.None, TimeSpan.FromSeconds(5)))
            .FirstOrDefault(m => m.Success)?
            .Groups["ns"].Value;

        Assert.False(string.IsNullOrEmpty(declaredNamespace), "No models namespace was generated.");

        foreach (var (hintName, source) in generatedSources)
        {
            foreach (Match match in Regex.Matches(source, @"[\w.]*\.Generated(?:\.[\w.]*)?\.Models\.Device\b", RegexOptions.None, TimeSpan.FromSeconds(5)))
            {
                Assert.Equal($"{declaredNamespace}.Device", match.Value);
            }

            Assert.False(
                source.Contains("Generated..", StringComparison.Ordinal),
                $"'{hintName}' contains an empty namespace segment ('Generated..').");
        }
    }

    private static List<(string HintName, string Source)> RunGenerator(
        IIncrementalGenerator generator,
        string assemblyName,
        string markerFileName,
        bool useFullReferences = false)
    {
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("Device.Management.spec.api.v1.yaml", DeviceYaml),
            new InMemoryAdditionalText(markerFileName, "{ }"));

        var compilation = CSharpCompilation.Create(
            assemblyName,
            references: useFullReferences
                ? CompilationVerificationHarness.GetFullFrameworkReferences()
                : CompilationVerificationHarness.GetMinimalReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts)
            .RunGeneratorsAndUpdateCompilation(
                compilation, out _, out _, CancellationToken.None);

        return driver
            .GetRunResult()
            .GeneratedTrees
            .Select(t => (HintName: Path.GetFileName(t.FilePath), Source: t.GetText().ToString()))
            .ToList();
    }
}