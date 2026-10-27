namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// What the robustness fuzz run did, for the report: per scenario its seed and every mutant - the mutation, how many
/// generators reported it and how many stayed silent, how many silent outputs were compiled, what the TypeScript
/// generator made of it, and any crash - plus the hostile marker files and overlays. Written to
/// <c>docs/test-suite-report.fuzz.json</c> when the class is done, only after a run over every scenario, and only
/// when the results changed (the file is tracked: an unchanged run leaves the tree clean).
/// </summary>
public sealed class FuzzResultsLog : IDisposable
{
    private readonly ConcurrentDictionary<string, JsonObject> scenarios = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, JsonObject> parts = new(StringComparer.Ordinal);
    private readonly ConcurrentBag<string> markerCrashes = [];
    private readonly ConcurrentBag<string> overlayCrashes = [];
    private int markers;
    private int overlays;

    public static string KindOf(string mutation)
        => mutation.Contains(" + ", StringComparison.Ordinal)
            ? "two at once"
            : mutation.Split(' ')[0].TrimEnd(':');

    public void AddScenario(
        string scenario,
        int seed,
        JsonArray mutants)
        => scenarios[scenario] = new JsonObject
        {
            ["name"] = scenario,
            ["seed"] = seed,
            ["mutants"] = mutants,
        };

    public void AddPart(
        string scenario,
        int mutants,
        int crashes)
        => parts[scenario] = new JsonObject { ["mutants"] = mutants, ["crashes"] = crashes };

    public void AddMarker(IEnumerable<string> crashes)
    {
        Interlocked.Increment(ref markers);
        foreach (var crash in crashes)
        {
            markerCrashes.Add(crash);
        }
    }

    public void AddOverlay(IEnumerable<string> crashes)
    {
        Interlocked.Increment(ref overlays);
        foreach (var crash in crashes)
        {
            overlayCrashes.Add(crash);
        }
    }

    public void Dispose()
    {
        // A run narrowed to one scenario (ATC_FUZZ_SCENARIO), or one that did not reach every scenario, is not the data
        var expected = GeneratorRobustnessFuzzTests.Scenarios().Count();
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ATC_FUZZ_SCENARIO")) || scenarios.Count != expected || scenarios.IsEmpty)
        {
            return;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "test", "Scenarios")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            return;
        }

        var data = new JsonObject
        {
            ["mutantsPerScenario"] = GeneratorRobustnessFuzzTests.MutantsPerScenario,
            ["compile"] = GeneratorRobustnessFuzzTests.CompileEverySilentMutant ? "every silent output" : "the first silent output per generator and scenario",
            ["generators"] = new JsonArray([.. GeneratorRobustnessFuzzTests.GeneratorMarkers.Select(m => (JsonNode)m), "TypeScript (Fetch and Axios)"]),
            ["scenarios"] = new JsonArray([.. scenarios
                .OrderBy(s => s.Key, StringComparer.Ordinal)
                .Select(s =>
                {
                    var node = (JsonObject)s.Value.DeepClone();
                    node["parts"] = parts.TryGetValue(s.Key, out var part) ? part.DeepClone() : null;
                    return (JsonNode)node;
                })]),
            ["markers"] = new JsonObject { ["cases"] = markers, ["crashes"] = new JsonArray([.. markerCrashes.Order(StringComparer.Ordinal).Select(c => (JsonNode)c)]) },
            ["overlays"] = new JsonObject { ["cases"] = overlays, ["crashes"] = new JsonArray([.. overlayCrashes.Order(StringComparer.Ordinal).Select(c => (JsonNode)c)]) },
        };

        var folder = Path.Combine(directory.FullName, "docs");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "test-suite-report.fuzz.json");
        if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject previous)
        {
            previous.Remove("generatedAt");
            if (JsonNode.DeepEquals(previous, data))
            {
                return;
            }
        }

        var written = new JsonObject { ["generatedAt"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) };
        foreach (var (key, value) in data.ToList())
        {
            data.Remove(key);
            written[key] = value;
        }

        File.WriteAllText(path, written.ToJsonString());
    }
}