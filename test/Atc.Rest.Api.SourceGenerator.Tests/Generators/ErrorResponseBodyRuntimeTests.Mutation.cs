namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The report's mutation score: how many of the code changes Stryker.NET makes to a product library its tests catch,
/// per library and per file, with the changes no test caught. A Stryker run takes long and is run by hand
/// (<c>dotnet stryker</c> in a test project with a <c>stryker-config.json</c>), so the summary is kept in
/// <c>docs/test-suite-report.mutation.json</c> with the run's own date and commit: a newer local run replaces it, an
/// older one does not.
/// </summary>
public sealed partial class ErrorResponseBodyRuntimeTests
{
    private const int MaxSurvivorsListed = 500;

    private static JsonObject? CollectMutation(string root)
    {
        var sidecarPath = Path.Combine(root, ReportFolderName, "test-suite-report.mutation.json");
        var sidecar = File.Exists(sidecarPath) ? JsonNode.Parse(File.ReadAllText(sidecarPath))?.AsObject() : null;
        var previous = (sidecar?["projects"]?.AsArray() ?? [])
            .Where(p => p is not null)
            .ToDictionary(p => (string)p!["testProject"]!, p => p!.AsObject(), StringComparer.Ordinal);

        var projects = new List<JsonObject>();
        foreach (var testProject in Directory.GetDirectories(Path.Combine(root, "test"))
            .Where(d => File.Exists(Path.Combine(d, "stryker-config.json")))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal))
        {
            var local = ReadStrykerRun(root, testProject!);
            var kept = previous.GetValueOrDefault(testProject!);
            var newer = local is not null && (kept is null || string.CompareOrdinal((string?)local["runAt"], (string?)kept["runAt"]) > 0);
            if ((newer ? local : kept) is { } project)
            {
                projects.Add((JsonObject)project.DeepClone());
            }
        }

        if (projects.Count == 0)
        {
            return null;
        }

        var data = new JsonObject { ["projects"] = new JsonArray([.. projects]) };
        if (sidecar is null || !JsonNode.DeepEquals(sidecar, data))
        {
            File.WriteAllText(sidecarPath, data.ToJsonString());
        }

        // How far each run is behind: the commits since the one it ran on
        foreach (var project in data["projects"]!.AsArray())
        {
            var commit = (string?)project!["commit"];
            if (commit is not null && int.TryParse(GitText(root, $"rev-list --count {commit}..HEAD")?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var behind))
            {
                project["commitsSince"] = behind;
            }
        }

        return data;
    }

    /// <summary>The newest Stryker report of a test project, summarized; null when it has none.</summary>
    private static JsonObject? ReadStrykerRun(
        string root,
        string testProject)
    {
        var output = Path.Combine(root, "test", testProject, "StrykerOutput");
        var report = Directory.Exists(output)
            ? Directory.GetDirectories(output)
                .Select(d => new FileInfo(Path.Combine(d, "reports", "mutation-report.json")))
                .Where(f => f.Exists)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()
            : null;
        if (report is null || JsonNode.Parse(File.ReadAllText(report.FullName)) is not JsonObject document)
        {
            return null;
        }

        var runAt = new DateTimeOffset(report.LastWriteTimeUtc, TimeSpan.Zero).ToLocalTime();
        var commit = GitText(root, $"log -1 --before={runAt.ToString("O", CultureInfo.InvariantCulture)} --format=%h")?.Trim();
        var sourceRoot = (string?)document["projectRoot"] ?? string.Empty;

        static bool Detected(string status)
            => status is "Killed" or "Timeout";
        static bool Missed(string status)
            => status is "Survived" or "NoCoverage";
        static double Score(
            int detected,
            int missed)
            => detected + missed == 0 ? 100 : System.Math.Round(100.0 * detected / (detected + missed), 1);

        var statuses = new Dictionary<string, int>(StringComparer.Ordinal);
        var files = new List<JsonObject>();
        var survivors = new List<JsonObject>();
        foreach (var (path, file) in document["files"]?.AsObject() ?? [])
        {
            var relative = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');
            var mutants = file?["mutants"]?.AsArray().Select(m => m!.AsObject()).ToList() ?? [];
            foreach (var mutant in mutants)
            {
                var status = (string?)mutant["status"] ?? "?";
                statuses[status] = statuses.GetValueOrDefault(status) + 1;
                if (status == "Survived")
                {
                    var replacement = (string?)mutant["replacement"] ?? string.Empty;
                    survivors.Add(new JsonObject
                    {
                        ["file"] = relative,
                        ["line"] = (int?)mutant["location"]?["start"]?["line"],
                        ["mutator"] = (string?)mutant["mutatorName"],
                        ["replacement"] = replacement.Length > 120 ? replacement[..120] + "..." : replacement,
                    });
                }
            }

            var detected = mutants.Count(m => Detected((string?)m["status"] ?? string.Empty));
            var missed = mutants.Count(m => Missed((string?)m["status"] ?? string.Empty));
            if (detected + missed > 0)
            {
                files.Add(new JsonObject
                {
                    ["file"] = relative,
                    ["detected"] = detected,
                    ["survived"] = mutants.Count(m => (string?)m["status"] == "Survived"),
                    ["noCoverage"] = mutants.Count(m => (string?)m["status"] == "NoCoverage"),
                    ["score"] = Score(detected, missed),
                });
            }
        }

        var killed = statuses.Where(s => Detected(s.Key)).Sum(s => s.Value);
        var survived = statuses.GetValueOrDefault("Survived");
        var noCoverage = statuses.GetValueOrDefault("NoCoverage");
        return new JsonObject
        {
            ["testProject"] = testProject,
            ["library"] = Path.GetFileName(sourceRoot.TrimEnd('\\', '/')),
            ["runAt"] = runAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            ["commit"] = string.IsNullOrEmpty(commit) ? null : commit,
            ["score"] = Score(killed, survived + noCoverage),
            ["coveredScore"] = Score(killed, survived),
            ["statuses"] = new JsonObject(statuses.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => new KeyValuePair<string, JsonNode?>(s.Key, s.Value))),
            ["files"] = new JsonArray([.. files.OrderBy(f => (double)f["score"]!).ThenBy(f => (string)f["file"]!, StringComparer.Ordinal)]),
            ["survivors"] = new JsonArray([.. survivors
                .OrderBy(s => (string)s["file"]!, StringComparer.Ordinal)
                .ThenBy(s => (int?)s["line"] ?? 0)
                .Take(MaxSurvivorsListed)]),
            ["survivorsListed"] = System.Math.Min(survivors.Count, MaxSurvivorsListed),
        };
    }
}