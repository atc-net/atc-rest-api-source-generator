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
    /// The remaining failures cluster into two families, neither introduced by this gate: a path
    /// segment whose name collides with a model of the same name, so the namespace shadows the type
    /// (<c>Session</c> in <c>CookieParameters</c>); and segment <c>Models</c> namespaces that are
    /// imported but never populated, which is what the OpenAPI 3.1/3.2 scenarios hit.
    /// </para>
    /// <para>
    /// The third family - polymorphic <c>oneOf</c>/<c>anyOf</c> bases that were referenced but never
    /// emitted - has been fixed, and its eight entries removed from this list by the ratchet.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> KnownNonCompilingCombinations = new(StringComparer.Ordinal)
    {
        "CachingHybrid/Server",
        "CookieParameters/Server",
        "CookieParameters/Client-Operation",
        "CookieParameters/Client-Typed",
        "ModelsAndProperties/Server",
        "ModelsAndProperties/Client-Typed",
        "OpenApi31Features/Server",
        "OpenApi31Features/Client-Typed",
        "OpenApi32Features/Server",
        "OpenApi32Features/Client-Typed",
        "PetStoreFull/Server",
        "PetStoreFull/Client-Operation",
        "PetStoreFull/Client-Typed",
        "SecurityHybrid/Server",
        "SecurityHybrid/Client-Typed",
        "SecurityStandard/Server",
        "SecurityStandard/Client-Typed",

        // Derivative: a ServerDomain compilation includes the server output it implements against,
        // so these four inherit the failure of their Server row above rather than adding one. They
        // will go green when that row does; there is nothing separate to fix here.
        "PetStoreFull/ServerDomain",
        "SecurityHybrid/ServerDomain",
        "SecurityStandard/ServerDomain",
    };

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

        var errors = CompilationVerificationHarness.CompileGeneratedSources(generatedSources);

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
}