namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The report's Test layers tab: per feature area of <see cref="FeatureLayerMap"/>, how many tests prove it at each
/// layer, counted from this run's data - the test suites' TRX results, the runtime matrices, the tsc gate and the
/// Playwright run - and the test classes no area claims.
/// </summary>
public sealed partial class ErrorResponseBodyRuntimeTests
{
    private static readonly (string Key, string Label, string Covers)[] TestLayers =
    [
        ("unit", "Unit", "The code that reads a specification and writes the code, tested piece by piece without a compilation."),
        ("generator", "Generator runs", "The Roslyn generators run on a project: the code compiled, often called over HTTP."),
        ("scenarios", "Scenarios", "The area's scenario specifications: every generator's snapshot, the compile gate, the overlay round trip, the integration tests."),
        ("matrices", "Runtime matrices", "Cases of the runtime matrices: the generated server on Kestrel called by every generated client."),
        ("typescript", "TypeScript gates", "Generated TypeScript compiled by tsc (every client, hook and option of the area's scenarios) and run against a server."),
        ("playwright", "Playwright", "The Showcase apps (Blazor and React) driven in a browser."),
    ];

    private static JsonObject CollectTestLayers(JsonObject matrix)
    {
        // Every test class of the run, with its project's short name and its layer
        var shortProject = FeatureLayerMap.Projects.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);
        var classes = new List<(string Project, string Area, JsonObject Node)>();
        foreach (var suite in matrix["testSuites"]?["suites"]?.AsArray() ?? [])
        {
            var project = shortProject.GetValueOrDefault((string?)suite!["project"] ?? string.Empty);
            if (project is null)
            {
                continue;
            }

            foreach (var area in suite["areas"]?.AsArray() ?? [])
            {
                foreach (var testClass in area!["classes"]?.AsArray() ?? [])
                {
                    classes.Add((project, (string?)area["name"] ?? string.Empty, testClass!.AsObject()));
                }
            }
        }

        static string LayerOf(
            string project,
            string area)
            => (project, area) switch
            {
                ("SourceGenerator", "Generators") => "generator",
                ("Cli", "Gates") => "typescript",
                _ => "unit",
            };

        static int Number(
            JsonNode? node,
            string key)
            => (int?)node?[key] ?? 0;

        var tsc = matrix["support"]?["tsc"];
        var tscCombinations = tsc?["combinations"]?.AsArray().Select(c => (string)c!).ToList() ?? [];
        var tscFailing = tsc?["failing"]?.AsArray().Select(c => (string?)c ?? string.Empty).ToList() ?? [];
        var playwrightFiles = matrix["playwright"]?["files"]?.AsObject();
        var claimed = new HashSet<JsonObject>(ReferenceEqualityComparer.Instance);
        var emptyMatrixSelectors = new List<string>();

        var areas = new JsonArray();
        foreach (var area in FeatureLayerMap.Areas)
        {
            var layers = TestLayers.ToDictionary(l => l.Key, _ => (Tests: 0, Failed: 0, Items: new JsonArray()), StringComparer.Ordinal);
            void Add(
                string layer,
                int tests,
                int failed,
                JsonObject item)
            {
                var (t, f, items) = layers[layer];
                item["tests"] = tests;
                item["failed"] = failed;
                items.Add(item);
                layers[layer] = (t + tests, f + failed, items);
            }

            // Test classes, each in the layer of its project and area
            var notInRun = new JsonArray();
            foreach (var selector in area.Classes)
            {
                var (project, glob) = FeatureLayerMap.Split(selector);
                var matched = classes
                    .Where(c => c.Project == project && FeatureLayerMap.Matches(glob!, (string)c.Node["name"]!))
                    .ToList();
                if (matched.Count == 0)
                {
                    notInRun.Add(selector);
                }

                foreach (var (_, testArea, node) in matched)
                {
                    claimed.Add(node);
                    Add(LayerOf(project, testArea), Number(node, "tests"), Number(node, "failed"), new JsonObject
                    {
                        ["name"] = (string?)node["name"],
                        ["project"] = project,
                    });
                }
            }

            // Scenarios: the per-scenario classes' cases for the area's scenarios
            foreach (var scenario in area.Scenarios)
            {
                var cases = classes.Select(c => c.Node["scenarios"]?[scenario]).Where(n => n is not null).ToList();
                Add("scenarios", cases.Sum(n => Number(n, "tests")), cases.Sum(n => Number(n, "failed")), new JsonObject { ["name"] = scenario });

                // ... and the tsc gate's combinations of each (scenario / client / hooks / options)
                var combinations = tscCombinations.Count(c => c.StartsWith(scenario + "/", StringComparison.Ordinal));
                if (combinations > 0)
                {
                    Add("typescript", combinations, tscFailing.Count(c => c.StartsWith(scenario + "/", StringComparison.Ordinal)), new JsonObject
                    {
                        ["name"] = $"tsc: {scenario}",
                        ["project"] = "Cli",
                    });
                }
            }

            // Runtime-matrix cases, a matrix or one group of it; a case fails when any client's verdict is fail. The
            // error-body matrix is the report's root, the others are under their key.
            foreach (var selector in area.Matrices)
            {
                var (key, group) = FeatureLayerMap.Split(selector);
                var source = key == "errorBodies" ? matrix : matrix[key];
                var cases = (source?["cases"]?.AsArray() ?? [])
                    .Where(c => group is null || string.Equals((string?)c!["group"], group, StringComparison.Ordinal))
                    .ToList();
                if (cases.Count == 0)
                {
                    emptyMatrixSelectors.Add($"{area.Name}: {selector}");
                }

                var failed = cases.Count(c => c!["clients"]?.AsObject().Any(v => (string?)v.Value?["verdict"] == "fail") == true);
                Add("matrices", cases.Count, failed, new JsonObject { ["name"] = selector });
            }

            // Playwright spec files, every app's tests in them
            foreach (var file in area.Playwright)
            {
                var tally = playwrightFiles?[file]?.AsObject();
                var tests = tally?.Sum(s => (int?)s.Value ?? 0) ?? 0;
                Add("playwright", tests, Number(tally, "unexpected"), new JsonObject { ["name"] = file });
            }

            areas.Add(new JsonObject
            {
                ["name"] = area.Name,
                ["covers"] = area.Covers,
                ["layers"] = new JsonObject(layers.Select(l => new KeyValuePair<string, JsonNode?>(l.Key, new JsonObject
                {
                    ["tests"] = l.Value.Tests,
                    ["failed"] = l.Value.Failed,
                    ["items"] = l.Value.Items,
                }))),
                ["notInRun"] = notInRun,
            });
        }

        // A matrix group no case is in: the map is behind the matrices - say which, rather than show an area with less
        Assert.True(emptyMatrixSelectors.Count == 0, "Matrix selectors in FeatureLayerMap with no case: " + string.Join("; ", emptyMatrixSelectors));

        var unclaimed = new JsonArray([.. classes
            .Where(c => !claimed.Contains(c.Node) && !FeatureLayerMap.PerScenarioClasses.Contains((string)c.Node["name"]!))
            .OrderBy(c => c.Project, StringComparer.Ordinal)
            .ThenBy(c => (string?)c.Node["name"], StringComparer.Ordinal)
            .Select(c => (JsonNode)new JsonObject
            {
                ["name"] = (string?)c.Node["name"],
                ["project"] = c.Project,
                ["layer"] = LayerOf(c.Project, c.Area),
                ["tests"] = Number(c.Node, "tests"),
            })]);

        return new JsonObject
        {
            ["layers"] = new JsonArray([.. TestLayers.Select(l => (JsonNode)new JsonObject { ["key"] = l.Key, ["label"] = l.Label, ["covers"] = l.Covers })]),
            ["areas"] = areas,
            ["unclaimed"] = unclaimed,
            ["testsInRun"] = classes.Sum(c => Number(c.Node, "tests")),
            ["testsClaimed"] = classes.Where(c => claimed.Contains(c.Node) || FeatureLayerMap.PerScenarioClasses.Contains((string)c.Node["name"]!)).Sum(c => Number(c.Node, "tests")),
        };
    }
}