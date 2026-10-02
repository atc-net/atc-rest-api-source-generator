namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Compiles the output of the real Roslyn generators for every scenario that declares a marker
/// file.
/// <para>
/// The <c>*.verified.cs</c> snapshot suite compares <b>text</b> and never compiles, so a snapshot
/// set can reference undefined types indefinitely - the blind spot that let the missing Models
/// <c>using</c> survive. This gate closes it: whatever a generator emits has to be a well-formed
/// compilation.
/// </para>
/// </summary>
[Trait("Category", "CompilationGate")]
public class GeneratorCompilationGateTests
{
    /// <summary>
    /// Scenario and master-folder pairs whose generated output does <b>not</b> compile today.
    /// <para>
    /// The gate was introduced long after these defects, so it starts as a ratchet rather than a
    /// clean sheet: every combination not listed here is enforced, and a listed combination that
    /// starts compiling <b>fails</b> the test asking to be removed. That way the list can only
    /// shrink - it never silently preserves debt someone has already paid off.
    /// </para>
    /// <para>
    /// <b>The list is empty and must stay empty for a release.</b> The families it once held, all fixed:
    /// polymorphic <c>oneOf</c> / <c>anyOf</c> bases that were referenced but never emitted; segments whose
    /// only models are inline schemas (emitted but not imported, on the server and in the typed client); a path
    /// segment named like a model, so the namespace shadowed the type (<c>Pet</c> under <c>/pet</c>); tuple
    /// schemas (<c>prefixItems</c>) that were never generated for a real OpenAPI 3.1 / 3.2 document; and a
    /// <c>float</c> default written without its suffix.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> KnownNonCompilingCombinations = new(StringComparer.Ordinal);

    public static IEnumerable<object[]> AllScenarioGenerators
        => GeneratorSnapshotHarness.GetScenarioGeneratorData();

    public static IEnumerable<object[]> CompilableScenarioGenerators
        => GeneratorSnapshotHarness.GetScenarioGeneratorData();

    /// <summary>
    /// Asserts the generator emitted something at all.
    /// <para>
    /// This guards the failure mode that driving snapshots from the generator introduces: a run
    /// that silently emits zero files would delete every baseline and leave all assertions passing
    /// vacuously. <c>EndpointPerOperation</c> does exactly that under a minimal reference set,
    /// where it aborts with <c>ATC_API_DEP003</c>.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarioGenerators))]
    public void Generator_EmitsSources(
        string scenarioName,
        string masterFolder)
    {
        var generatedSources = GeneratorSnapshotHarness.RunRaw(scenarioName, masterFolder);

        Assert.False(
            generatedSources.Count == 0,
            $"{scenarioName}/{masterFolder}: the generator emitted no sources.");
    }

    /// <summary>
    /// Compiles the emitted sources as a real assembly and asserts there are no compile errors,
    /// except for the combinations listed in <see cref="KnownNonCompilingCombinations"/>, which are
    /// asserted to still fail so the list stays honest.
    /// </summary>
    [Theory]
    [MemberData(nameof(CompilableScenarioGenerators))]
    public void GeneratedSources_Compile(
        string scenarioName,
        string masterFolder)
    {
        var combination = $"{scenarioName}/{masterFolder}";
        var generatedSources = GeneratorSnapshotHarness.RunRawForCompilation(scenarioName, masterFolder);

        Assert.False(
            generatedSources.Count == 0,
            $"{combination}: the generator emitted no sources.");

        var errors = CompilationVerificationHarness.CompileGeneratedSources(
            generatedSources,
            CompilationVerificationHarness.ScenarioReferencesMinimalApi(scenarioName));

        if (KnownNonCompilingCombinations.Contains(combination))
        {
            Assert.False(
                errors.Count == 0,
                $"{combination} now compiles. Remove it from {nameof(KnownNonCompilingCombinations)}.");

            return;
        }

        Assert.True(
            errors.Count == 0,
            $"{combination}: {errors.Count} compile error(s):\n  " +
            string.Join("\n  ", errors.Take(25)));
    }

    /// <summary>
    /// Compiles the emitted sources the way a strict consumer does (nullable enabled, XML documentation
    /// checked) and asserts there are no compiler warnings: with <c>TreatWarningsAsErrors</c> a warning in
    /// generated code breaks the consumer's build, and the consumer cannot fix generated code.
    /// </summary>
    [Theory]
    [MemberData(nameof(CompilableScenarioGenerators))]
    public void GeneratedSources_CompileWithoutWarnings(
        string scenarioName,
        string masterFolder)
    {
        var combination = $"{scenarioName}/{masterFolder}";
        var generatedSources = GeneratorSnapshotHarness.RunRawForCompilation(scenarioName, masterFolder);

        var warnings = CompilationVerificationHarness.CompileGeneratedSourcesForWarnings(
            generatedSources,
            CompilationVerificationHarness.ScenarioReferencesMinimalApi(scenarioName));

        Assert.True(
            warnings.Count == 0,
            $"{combination}: {warnings.Count} compiler warning(s):\n  " +
            string.Join("\n  ", warnings.Take(25)));
    }

    /// <summary>
    /// Scenario and generator pairs <b>without a marker folder</b> whose output does not compile today - the
    /// ratchet of <see cref="EveryScenario_CompilesForEveryGenerator"/>, with the same rule as
    /// <see cref="KnownNonCompilingCombinations"/>: a listed pair that starts compiling fails the test, so the
    /// list can only shrink.
    /// <para>
    /// <b>The list is empty and must stay empty for a release.</b> The families it once held, all fixed: a
    /// component that extends the generic page (<c>AccountPage: allOf [PaginatedResult, results: Account[]]</c>)
    /// derived from <c>PaginatedResult</c> without its type argument; an inline response that extends a base
    /// which is <i>not</i> a generic page was typed <c>PaginationResult&lt;Item&gt;</c>; an inline enum on a
    /// query parameter of a client with one flat namespace was generated in a namespace nothing imported; and a
    /// streamed operation whose response is not an array got an EndpointPerOperation endpoint that referenced
    /// a result class that is not generated.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> KnownNonCompilingCombinationsWithoutMarker = new(StringComparer.Ordinal);

    public static IEnumerable<object[]> ScenarioGeneratorsWithoutMarker
        => GeneratorSnapshotHarness.GetScenarioGeneratorDataWithoutMarker();

    /// <summary>
    /// Every scenario is given to every C# generator, also the ones it has no marker folder (and so no
    /// snapshots) for: a specification written to exercise one generator is still a specification a consumer
    /// could give to another. Each is generated with the default marker and has to compile without errors and
    /// without warnings.
    /// </summary>
    [Theory]
    [MemberData(nameof(ScenarioGeneratorsWithoutMarker))]
    public void EveryScenario_CompilesForEveryGenerator(
        string scenarioName,
        string masterFolder)
    {
        var combination = $"{scenarioName}/{masterFolder} (default marker)";
        var generatedSources = GeneratorSnapshotHarness.RunRawForCompilation(scenarioName, masterFolder);

        Assert.False(
            generatedSources.Count == 0,
            $"{combination}: the generator emitted no sources.");

        var includeMinimalApi = CompilationVerificationHarness.ScenarioReferencesMinimalApi(scenarioName);
        var errors = CompilationVerificationHarness.CompileGeneratedSources(generatedSources, includeMinimalApi);

        if (KnownNonCompilingCombinationsWithoutMarker.Contains($"{scenarioName}/{masterFolder}"))
        {
            Assert.False(
                errors.Count == 0,
                $"{combination} now compiles. Remove it from {nameof(KnownNonCompilingCombinationsWithoutMarker)}.");

            return;
        }

        Assert.True(
            errors.Count == 0,
            $"{combination}: {errors.Count} compile error(s):\n  " +
            string.Join("\n  ", errors.Take(25)));

        var warnings = CompilationVerificationHarness.CompileGeneratedSourcesForWarnings(generatedSources, includeMinimalApi);

        Assert.True(
            warnings.Count == 0,
            $"{combination}: {warnings.Count} compiler warning(s):\n  " +
            string.Join("\n  ", warnings.Take(25)));
    }
}