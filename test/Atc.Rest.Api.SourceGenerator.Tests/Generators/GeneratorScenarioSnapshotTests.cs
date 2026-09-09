namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Snapshots the output of the real Roslyn generators for every scenario.
/// <para>
/// These snapshots previously came from <c>GeneratorTestHelper</c> in the integration-test project,
/// which drove its own sequence of <c>CodeGenerationService</c> calls. That was a second
/// implementation of generation, and it had drifted: it never emitted the built-in
/// <c>ProblemDetails</c> / <c>ValidationProblemDetails</c> / <c>Constants</c> contracts, and it
/// emitted a single client per specification where the generator emits one per path segment.
/// Driving the snapshots from the generator makes that class of drift impossible.
/// </para>
/// <para>
/// A snapshot is named after the generator hint name, which is the fully-qualified type name. The
/// old layout nested files under <c>Contracts/{Segment}/</c> folders, but folder placement is a
/// scaffolding concern the generator has no notion of - the hint name is the only identity it
/// actually assigns.
/// </para>
/// </summary>
[Trait("Category", "Snapshot")]
public class GeneratorScenarioSnapshotTests
{
    public static IEnumerable<object[]> AllScenarioGenerators
        => GeneratorSnapshotHarness.GetConvertedScenarioGeneratorData();

    public static IEnumerable<object[]> AllSnapshots
        => GeneratorSnapshotHarness.GetConvertedSnapshotData();

    /// <summary>
    /// Verifies one emitted source against its snapshot.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllSnapshots))]
    public Task Generator_MatchesSnapshot(
        string scenarioName,
        string masterFolder,
        string snapshotName)
    {
        var snapshot = GeneratorSnapshotHarness
            .RunCached(scenarioName, masterFolder)
            .Single(s => string.Equals(s.Name, snapshotName, StringComparison.Ordinal));

        return Verifier
            .Verify(snapshot.Source, "cs")
            .UseDirectory(GeneratorSnapshotHarness.GetSnapshotDirectory(scenarioName, masterFolder))
            .UseFileName(snapshot.Name)
            .DisableDiff();
    }

    /// <summary>
    /// Asserts the generator emitted something at all, so an empty run cannot verify an empty set.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarioGenerators))]
    public void Generator_EmitsSnapshots(
        string scenarioName,
        string masterFolder)
    {
        var snapshots = GeneratorSnapshotHarness.RunCached(scenarioName, masterFolder);

        Assert.False(
            snapshots.Count == 0,
            $"{scenarioName}/{masterFolder}: the generator emitted no sources.");
    }

    /// <summary>
    /// Asserts that no snapshot file survives that the generator no longer emits.
    /// <para>
    /// Verify only writes and compares; it never deletes. Without this, a type that stops being
    /// generated would leave its snapshot behind forever, and the suite would keep passing while
    /// documenting output that no longer exists.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarioGenerators))]
    public void Snapshots_HaveNoOrphans(
        string scenarioName,
        string masterFolder)
    {
        var directory = GeneratorSnapshotHarness.GetSnapshotDirectory(scenarioName, masterFolder);

        if (!Directory.Exists(directory))
        {
            return;
        }

        var expected = GeneratorSnapshotHarness
            .RunCached(scenarioName, masterFolder)
            .Select(s => s.Name + ".verified.cs")
            .ToHashSet(StringComparer.Ordinal);

        var orphans = Directory
            .EnumerateFiles(directory, "*.verified.cs", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(n => !expected.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            orphans.Count == 0,
            $"{scenarioName}/{masterFolder}: {orphans.Count} snapshot(s) no longer emitted by the " +
            $"generator:\n  {string.Join("\n  ", orphans)}");
    }
}