namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The trim and Native AOT warnings in the generated code of every scenario, held to a ratchet: a combination may
/// not get more warnings than <c>AotAnalyzerBaseline.json</c> records, and one that gets fewer fails until the
/// baseline is lowered (run with <c>ATC_UPDATE_AOT_BASELINE=1</c>, which rewrites the file). The goal is an
/// empty baseline: generated code a project with <c>IsAotCompatible</c> builds without IL warnings.
/// </summary>
[Trait("Category", "CompilationGate")]
public class GeneratedCodeAotGateTests
{
    private static string BaselinePath
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Generators", "AotAnalyzerBaseline.json"));

    [Fact]
    public async Task GeneratedCode_AotAnalyzerWarnings_OnlyGoDown()
    {
        var combinations = GeneratorSnapshotHarness
            .GetScenarioGeneratorData()
            .Select(row => (Scenario: (string)row[0], Folder: (string)row[1]))
            .ToList();

        var actual = new System.Collections.Concurrent.ConcurrentDictionary<string, List<string>>(StringComparer.Ordinal);
        await Parallel.ForEachAsync(
            combinations,
            TestContext.Current.CancellationToken,
            async (combination, _) =>
            {
                var sources = GeneratorSnapshotHarness.RunRawForCompilation(combination.Scenario, combination.Folder);
                actual[$"{combination.Scenario}/{combination.Folder}"] = await AotAnalyzerHarness.AnalyzeAsync(
                    sources,
                    CompilationVerificationHarness.ScenarioReferencesMinimalApi(combination.Scenario));
            });

        var counts = actual
            .Where(pair => pair.Value.Count > 0)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.Ordinal);

        // Every warning, for working through them: ATC_AOT_DUMP=<file>
        if (Environment.GetEnvironmentVariable("ATC_AOT_DUMP") is { Length: > 0 } dumpPath)
        {
            await File.WriteAllLinesAsync(
                dumpPath,
                actual.OrderBy(pair => pair.Key, StringComparer.Ordinal).SelectMany(pair => pair.Value.Select(line => $"{pair.Key} | {line}")),
                TestContext.Current.CancellationToken);
        }

        if (Environment.GetEnvironmentVariable("ATC_UPDATE_AOT_BASELINE") == "1")
        {
            await File.WriteAllTextAsync(
                BaselinePath,
                JsonSerializer.Serialize(counts, new JsonSerializerOptions { WriteIndented = true }) + "\n",
                TestContext.Current.CancellationToken);
            return;
        }

        var baseline = JsonSerializer.Deserialize<Dictionary<string, int>>(
            await File.ReadAllTextAsync(BaselinePath, TestContext.Current.CancellationToken)) ?? [];

        var worse = counts
            .Where(pair => pair.Value > baseline.GetValueOrDefault(pair.Key))
            .Select(pair => $"{pair.Key}: {pair.Value} (baseline {baseline.GetValueOrDefault(pair.Key)})\n    " +
                            string.Join("\n    ", actual[pair.Key].Take(10)))
            .ToList();
        Assert.True(worse.Count == 0, "More trim/AOT warnings than the baseline:\n" + string.Join("\n", worse));

        var better = baseline
            .Where(pair => counts.GetValueOrDefault(pair.Key) < pair.Value)
            .Select(pair => $"{pair.Key}: {counts.GetValueOrDefault(pair.Key)} (baseline {pair.Value})")
            .ToList();
        Assert.True(
            better.Count == 0,
            "Fewer trim/AOT warnings than the baseline - lower it (run with ATC_UPDATE_AOT_BASELINE=1):\n" + string.Join("\n", better));
    }
}