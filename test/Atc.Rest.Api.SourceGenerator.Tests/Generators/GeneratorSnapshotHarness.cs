namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Snapshot and compilation infrastructure driven by the <b>real</b> Roslyn generators.
/// <para>
/// Historically the <c>*.verified.cs</c> snapshots were produced by <c>GeneratorTestHelper</c> in
/// the integration-test project, which hand-rolled its own sequence of <c>CodeGenerationService</c>
/// calls. That duplicated the orchestration performed by the <c>Api*Generator</c> classes and
/// drifted from it - most visibly, it never emitted the built-in <c>ProblemDetails</c> /
/// <c>ValidationProblemDetails</c> / <c>Constants</c> contracts, so snapshots referenced types no
/// snapshot file defined.
/// </para>
/// <para>
/// This harness removes the second implementation: snapshots are taken from whatever the generator
/// actually emits, so the two cannot diverge by construction.
/// </para>
/// </summary>
internal static class GeneratorSnapshotHarness
{
    private const string GeneratedFileSuffix = ".g.cs";

    private static readonly ConcurrentDictionary<string, IReadOnlyList<GeneratedSnapshot>> SnapshotCache = new(StringComparer.Ordinal);

    /// <summary>The master folder holding the <c>EndpointPerOperation</c> client marker.</summary>
    public const string ClientOperationMasterFolder = "Client-Operation";

    /// <summary>The master folder holding the <c>TypedClient</c> client marker.</summary>
    public const string ClientTypedMasterFolder = "Client-Typed";

    /// <summary>The master folder holding the server marker.</summary>
    public const string ServerMasterFolder = "Server";

    /// <summary>The master folder holding the server-handlers marker.</summary>
    public const string ServerDomainMasterFolder = "ServerDomain";

    /// <summary>The client marker file name.</summary>
    public const string ClientMarkerFileName = ".atc-rest-api-client";

    /// <summary>
    /// The master folders that map onto a Roslyn generator. <c>TS-Client-*</c> is deliberately
    /// absent: there is no TypeScript incremental generator, so that path cannot be converged and
    /// stays on <c>CodeGenerationService</c>.
    /// </summary>
    public static IReadOnlyList<string> MasterFolders { get; } =
    [
        ServerMasterFolder,
        ServerDomainMasterFolder,
        ClientOperationMasterFolder,
        ClientTypedMasterFolder,
        ClientTypedMasterFolder,
    ];

    /// <summary>
    /// The master folders whose <c>*.verified.cs</c> snapshots are produced by the Roslyn generator
    /// rather than by <c>GeneratorTestHelper</c>.
    /// <para>
    /// The move happens one folder at a time, because each one re-bases hundreds of snapshot files
    /// and the diffs are only reviewable in isolation. A folder listed here is skipped by
    /// <c>ScenarioTests</c> in the integration-test project, so exactly one suite owns it.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> ConvertedMasterFolders { get; } =
    [
        ClientOperationMasterFolder,
        ClientTypedMasterFolder,
    ];

    /// <summary>
    /// Enumerates every scenario and converted master-folder pair.
    /// </summary>
    public static IEnumerable<object[]> GetConvertedScenarioGeneratorData()
        => ConvertedMasterFolders.SelectMany(
            masterFolder => GetScenarioNames(masterFolder),
            (masterFolder, scenarioName) => new object[] { scenarioName, masterFolder });

    /// <summary>
    /// Enumerates one entry per emitted source across the converted master folders, so each
    /// snapshot is its own test case.
    /// <para>
    /// Verifying several files inside a single test would stop at the first mismatch and hide the
    /// rest, which makes a re-baseline impossible to review in one pass.
    /// </para>
    /// </summary>
    public static IEnumerable<object[]> GetConvertedSnapshotData()
        => GetConvertedScenarioGeneratorData()
            .SelectMany(row => RunCached((string)row[0], (string)row[1])
                .Select(snapshot => new object[] { row[0], row[1], snapshot.Name }));

    /// <summary>
    /// Runs the generator for a scenario and master folder, memoizing the result.
    /// <para>
    /// With one test per emitted file the same generator run is otherwise repeated hundreds of
    /// times over the same inputs.
    /// </para>
    /// </summary>
    public static IReadOnlyList<GeneratedSnapshot> RunCached(
        string scenarioName,
        string masterFolder)
        => SnapshotCache.GetOrAdd(
            $"{scenarioName}/{masterFolder}",
            _ => Run(scenarioName, masterFolder));

    /// <summary>
    /// Runs the generator owning <paramref name="masterFolder"/> for a scenario and returns the
    /// emitted sources keyed by a snapshot name derived from the generator hint name.
    /// </summary>
    public static IReadOnlyList<GeneratedSnapshot> Run(
        string scenarioName,
        string masterFolder = ClientOperationMasterFolder)
        => RunRaw(scenarioName, masterFolder)
            .Select(s => new GeneratedSnapshot(ToSnapshotName(s.HintName), s.Source))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Runs the generator owning <paramref name="masterFolder"/> and returns the raw sources, for
    /// callers that need to compile them.
    /// <para>
    /// Always uses the full reference set: with minimal references <c>EndpointPerOperation</c> mode
    /// aborts with <c>ATC_API_DEP003</c> and emits <b>zero</b> files, which would make every
    /// assertion pass vacuously.
    /// </para>
    /// </summary>
    public static List<(string HintName, string Source)> RunRaw(
        string scenarioName,
        string masterFolder = ClientOperationMasterFolder)
    {
        var (generator, markerFileName) = ResolveGenerator(masterFolder);

        // The server-domain generator derives its namespace from the compilation assembly name
        // while the other generators derive it from the specification. Naming the compilation after
        // the scenario mirrors a single project built from that spec, so the handler scaffolds land
        // in the same root namespace as the interfaces they implement.
        return CompilationVerificationHarness.RunGenerator(
            generator,
            scenarioName,
            GetYamlFileName(scenarioName),
            markerFileName,
            masterFolder,
            useFullReferences: true,
            assemblyName: scenarioName).GeneratedSources;
    }

    /// <summary>
    /// Runs the generator owning <paramref name="masterFolder"/> together with everything else that
    /// has to be present for the output to be a self-contained compilation.
    /// <para>
    /// Only <c>ServerDomain</c> differs from <see cref="RunRaw"/>: the handler scaffolds
    /// <i>implement</i> the <c>I{Operation}Handler</c> interfaces and consume the parameter/result
    /// records, all of which the <b>server</b> generator emits. Compiling the domain output alone
    /// would report a missing type for every handler - an artifact of the split, not a defect.
    /// </para>
    /// </summary>
    public static List<(string HintName, string Source)> RunRawForCompilation(
        string scenarioName,
        string masterFolder)
    {
        var sources = RunRaw(scenarioName, masterFolder);

        if (!string.Equals(masterFolder, ServerDomainMasterFolder, StringComparison.Ordinal))
        {
            return sources;
        }

        var serverSources = RunRaw(scenarioName, ServerMasterFolder);

        return serverSources
            .Concat(sources)
            .ToList();
    }

    /// <summary>
    /// Maps a master folder onto the incremental generator that owns it and the marker file that
    /// triggers it.
    /// </summary>
    public static (IIncrementalGenerator Generator, string MarkerFileName) ResolveGenerator(
        string masterFolder)
        => masterFolder switch
        {
            ServerMasterFolder => (new ApiServerGenerator(), ".atc-rest-api-server"),
            ServerDomainMasterFolder => (new ApiServerDomainGenerator(), ".atc-rest-api-server-handlers"),
            ClientOperationMasterFolder or ClientTypedMasterFolder => (new ApiClientGenerator(), ClientMarkerFileName),
            _ => throw new ArgumentOutOfRangeException(
                nameof(masterFolder),
                masterFolder,
                "No Roslyn generator owns this master folder."),
        };

    /// <summary>
    /// Enumerates scenario names that declare a marker file for the given master folder.
    /// <para>
    /// <c>ScenarioDiscovery</c> lives in the integration-test project, which deliberately carries no
    /// Roslyn dependency, so discovery is re-done here against the same on-disk layout.
    /// </para>
    /// </summary>
    public static IEnumerable<string> GetScenarioNames(
        string masterFolder = ClientOperationMasterFolder)
    {
        var (_, markerFileName) = ResolveGenerator(masterFolder);

        return new DirectoryInfo(GetScenariosRoot())
            .EnumerateDirectories()
            .Where(d => File.Exists(Path.Combine(d.FullName, masterFolder, markerFileName)))
            .Select(d => d.Name)
            .OrderBy(n => n, StringComparer.Ordinal);
    }

    /// <summary>
    /// Enumerates every scenario and master-folder pair that a Roslyn generator owns.
    /// </summary>
    public static IEnumerable<object[]> GetScenarioGeneratorData()
        => MasterFolders.SelectMany(
            masterFolder => GetScenarioNames(masterFolder),
            (masterFolder, scenarioName) => new object[] { scenarioName, masterFolder });

    /// <summary>
    /// Gets the directory holding the snapshots for a scenario and master folder.
    /// </summary>
    public static string GetSnapshotDirectory(
        string scenarioName,
        string masterFolder)
        => Path.Combine(GetScenariosRoot(), scenarioName, masterFolder);

    /// <summary>
    /// Gets the absolute path to the source <c>test/Scenarios</c> directory.
    /// </summary>
    public static string GetScenariosRoot()
        => Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Scenarios"));

    /// <summary>
    /// Converts a generator hint name into a stable, flat snapshot file name.
    /// <para>
    /// The hint name arrives as a path (Roslyn exposes it via the generated tree file path), and
    /// already encodes the full namespace - for example
    /// <c>InlineSchemas.Generated.Reports.Models.GetReportResponse.g.cs</c>. That fully-qualified
    /// name is the snapshot identity: folder placement is a CLI-scaffolding concern the generator
    /// has no notion of.
    /// </para>
    /// </summary>
    public static string ToSnapshotName(string hintName)
    {
        var fileName = Path.GetFileName(hintName);

        return fileName.EndsWith(GeneratedFileSuffix, StringComparison.Ordinal)
            ? fileName[..^GeneratedFileSuffix.Length]
            : fileName;
    }

    private static string GetYamlFileName(string scenarioName)
    {
        var scenarioDirectory = Path.Combine(GetScenariosRoot(), scenarioName);

        var yamlFiles = new DirectoryInfo(scenarioDirectory)
            .EnumerateFiles("*.yaml")
            .Concat(new DirectoryInfo(scenarioDirectory).EnumerateFiles("*.yml"))
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        return yamlFiles.Count switch
        {
            1 => yamlFiles[0],
            0 => throw new FileNotFoundException(
                $"Scenario '{scenarioName}' has no .yaml/.yml specification."),
            _ => yamlFiles.FirstOrDefault(n =>
                     string.Equals(
                         Path.GetFileNameWithoutExtension(n),
                         scenarioName,
                         StringComparison.OrdinalIgnoreCase))
                 ?? yamlFiles[0],
        };
    }
}