namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Applying an overlay goes through the document as JSON (and, for a multi-part specification, through the merged
/// document written back out). This proves the round trip is lossless: every scenario, with every generator that
/// has a marker for it, generates byte for byte the same code with an overlay that changes nothing as without one.
/// </summary>
[Trait("Category", "CompilationGate")]
public sealed class OverlayRoundTripTests
{
    private const string NoOpOverlay = """
        overlay: 1.2.0
        info:
          title: Changes nothing
          version: 1.0.0
        actions:
          - target: $['x-no-such-member']
            update: { description: never applied }
        """;

    public static IEnumerable<object[]> ScenarioGenerators
        => GeneratorSnapshotHarness.GetScenarioGeneratorData()
            .Where(d => !string.Equals((string)d[1], "ServerDomain", StringComparison.Ordinal));

    [Theory]
    [MemberData(nameof(ScenarioGenerators))]
    public void OverlayThatChangesNothing_GeneratesExactlyTheSameCode(
        string scenarioName,
        string masterFolder)
    {
        var without = Run(scenarioName, masterFolder, withOverlay: false);
        var with = Run(scenarioName, masterFolder, withOverlay: true);

        Assert.NotEmpty(without.Sources);
        Assert.Contains(with.Diagnostics, d => d.Id == "ATC_API_GEN017");
        Assert.Equal(without.Sources.Select(s => s.HintName), with.Sources.Select(s => s.HintName), StringComparer.Ordinal);
        foreach (var (hintName, source) in without.Sources)
        {
            Assert.True(
                string.Equals(source, with.Sources.Single(s => s.HintName == hintName).Source, StringComparison.Ordinal),
                $"{scenarioName}/{masterFolder}: {hintName} differs when an overlay is applied");
        }

        Assert.Equal(
            without.Diagnostics.Select(d => d.Id).Order(StringComparer.Ordinal),
            with.Diagnostics.Where(d => d.Id != "ATC_API_GEN017").Select(d => d.Id).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    private static (List<(string HintName, string Source)> Sources, ImmutableArray<Diagnostic> Diagnostics) Run(
        string scenarioName,
        string masterFolder,
        bool withOverlay)
    {
        var (generator, markerFileName) = masterFolder switch
        {
            "Server" => ((IIncrementalGenerator)new ApiServerGenerator(), ".atc-rest-api-server"),
            _ => (new ApiClientGenerator(), ".atc-rest-api-client"),
        };

        var (diagnostics, sources) = CompilationVerificationHarness.RunGenerator(
            generator,
            scenarioName,
            GeneratorSnapshotHarness.GetYamlFileName(scenarioName),
            markerFileName,
            masterFolder,
            useFullReferences: true,
            assemblyName: scenarioName,
            markerTransform: withOverlay ? WithOverlay : (Func<string, string>?)null,
            extraFiles: withOverlay ? [("NoOp.overlay.yaml", NoOpOverlay)] : (IReadOnlyList<(string Path, string Content)>?)null);

        return (sources, diagnostics);
    }

    private static string WithOverlay(string markerContent)
    {
        var marker = JsonNode.Parse(markerContent)!.AsObject();
        var overlays = marker["overlays"] as JsonArray ?? [];
        overlays.Add("NoOp.overlay.yaml");
        marker["overlays"] = overlays.DeepClone();
        return marker.ToJsonString();
    }
}