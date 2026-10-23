namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The report's Test suites tab: every test of the five test projects as the last run recorded it (their TRX files in
/// <c>TestResults/</c>, written by <c>dotnet test --report-trx</c>), as suite, area (the test namespace), class and
/// test method - a theory's cases counted under their method - with the rules each class covers.
/// </summary>
public sealed partial class ErrorResponseBodyRuntimeTests
{
    private static readonly (string Project, string Covers)[] TestSuiteProjects =
    [
        ("Atc.OpenApi.Tests", "How a specification is read: the OpenAPI extensions and helpers every generator builds on (schemas, operations, parameters, media types, references)."),
        ("Atc.Rest.Api.Generator.Tests", "What is generated and checked: the extractors that write the code, the validator's rules, the services that load and merge a specification, and their helpers - all without Roslyn."),
        ("Atc.Rest.Api.SourceGenerator.Tests", "The source generators in a real compilation: every scenario's generated code against its snapshot, the diagnostics, and every generated client against the generated server over HTTP (the runtime matrices)."),
        ("Atc.Rest.Api.Generator.Cli.Tests", "The CLI: every command, the TypeScript client generator, and the gates that compile the TypeScript (tsc) and run it against a server."),
        ("Atc.Rest.Api.Generator.IntegrationTests", "The scenarios end to end: each scenario's generated C# and TypeScript against its snapshot, and package installs."),
    ];

    private static readonly Dictionary<string, string> TestAreaCovers = new(StringComparer.Ordinal)
    {
        ["Extensions"] = "Extension methods and properties on the OpenAPI and code-generation types.",
        ["Helpers"] = "Small rules the rest builds on (names, wire values, media types, paths, conventions).",
        ["Extractors"] = "The code each extractor writes: models, parameters, results, endpoints, clients, handlers.",
        ["Validators"] = "The specification rules (ATC_API_*): each rule's positive and negative cases.",
        ["Services"] = "Loading, merging and validating a specification; planning what is generated.",
        ["Models"] = "The records the generators pass around, and the rules they carry.",
        ["Generators"] = "The Roslyn generators run on real projects: snapshots, diagnostics and the runtime matrices.",
        ["Commands"] = "Each CLI command run as a process, with its options and its output.",
        ["Gates"] = "Generated TypeScript compiled by tsc and run against a server.",
        ["Options"] = "The options file and the command-line settings.",
        ["Schemas"] = "The JSON schemas of the marker and options files.",
        ["Configurations"] = "The marker files' configuration and its defaults.",
        ["Overlays"] = "OpenAPI Overlays applied to a specification.",
        ["JsonConverters"] = "The JSON converters the configuration files are read with.",
        ["Serialization"] = "How generated models and values are written to and read from JSON.",
        ["(root)"] = "Tests at the project's root namespace.",
    };

    private static JsonObject? CollectTestSuites(string root)
    {
        var folder = Path.Combine(root, "TestResults");
        if (!Directory.Exists(folder))
        {
            return null;
        }

        var commitTime = DateTimeOffset.TryParse(RunTool("git", "log -1 --format=%cI", root).Output.Trim().Split('\n')[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var committed)
            ? committed
            : DateTimeOffset.MaxValue;
        var rulesByClass = Validators.RuleCoverageTests.GetRuleTests()
            .SelectMany(r => r.Value.Select(c => (Class: c, Rule: r.Key)))
            .GroupBy(x => x.Class, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Rule).OrderBy(r => r, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);

        var suites = new JsonArray();
        foreach (var (project, covers) in TestSuiteProjects)
        {
            var trx = Directory
                .GetFiles(folder, project + "_*.trx")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            suites.Add(trx is null
                ? new JsonObject { ["project"] = project, ["covers"] = covers, ["missing"] = true }
                : ReadTestSuite(project, covers, trx.FullName, commitTime, rulesByClass));
        }

        return new JsonObject { ["suites"] = suites };
    }

    private static JsonObject ReadTestSuite(
        string project,
        string covers,
        string trxPath,
        DateTimeOffset commitTime,
        Dictionary<string, List<string>> rulesByClass)
    {
        var document = System.Xml.Linq.XDocument.Load(trxPath);
        System.Xml.Linq.XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var methods = document
            .Descendants(ns + "UnitTest")
            .Select(u => (Id: (string?)u.Attribute("id"), Case: (string?)u.Attribute("name"), Method: u.Element(ns + "TestMethod")))
            .Where(u => u.Id is not null && u.Method is not null)
            .ToDictionary(
                u => u.Id!,
                u => (Class: (string?)u.Method!.Attribute("className") ?? "?", Name: (string?)u.Method!.Attribute("name") ?? "?", Case: u.Case ?? string.Empty),
                StringComparer.Ordinal);

        var results = document
            .Descendants(ns + "UnitTestResult")
            .Select(r => (
                Test: methods.GetValueOrDefault((string?)r.Attribute("testId") ?? string.Empty),
                Outcome: (string?)r.Attribute("outcome") ?? "Unknown",
                Milliseconds: TimeSpan.TryParse((string?)r.Attribute("duration"), CultureInfo.InvariantCulture, out var duration) ? duration.TotalMilliseconds : 0))
            .Where(r => r.Test.Class is not null)
            .ToList();

        static string? ScenarioOf(string testCase)
        {
            var match = Regex.Match(testCase, @"\b(scenarioName|scenario): ""(?<s>[\w.-]+)""", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
            return match.Success ? match.Groups["s"].Value : null;
        }

        string AreaOf(string className)
        {
            var local = className.StartsWith(project + ".", StringComparison.Ordinal) ? className[(project.Length + 1)..] : className;
            var dot = local.IndexOf('.', StringComparison.Ordinal);
            return dot < 0 ? "(root)" : local[..dot];
        }

        JsonObject Tally(
            IEnumerable<(string Outcome, double Milliseconds)> items,
            JsonObject node)
        {
            var list = items.ToList();
            node["tests"] = list.Count;
            node["passed"] = list.Count(i => i.Outcome == "Passed");
            node["failed"] = list.Count(i => i.Outcome is "Failed" or "Error" or "Timeout" or "Aborted");
            node["skipped"] = list.Count(i => i.Outcome is "NotExecuted" or "Inconclusive" or "Skipped");
            node["ms"] = System.Math.Round(list.Sum(i => i.Milliseconds));
            return node;
        }

        var areas = new JsonArray();
        foreach (var area in results.GroupBy(r => AreaOf(r.Test.Class), StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var classes = new JsonArray();
            foreach (var testClass in area.GroupBy(r => r.Test.Class, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var classMethods = new JsonArray();
                foreach (var method in testClass.GroupBy(r => r.Test.Name, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
                {
                    classMethods.Add(Tally(method.Select(m => (m.Outcome, m.Milliseconds)), new JsonObject { ["name"] = method.Key }));
                }

                var shortName = testClass.Key[(testClass.Key.LastIndexOf('.') + 1)..];
                var node = Tally(testClass.Select(m => (m.Outcome, m.Milliseconds)), new JsonObject
                {
                    ["name"] = shortName,
                    ["fullName"] = testClass.Key,
                    ["rules"] = new JsonArray([.. rulesByClass.GetValueOrDefault(testClass.Key, []).Select(r => (JsonNode)r)]),
                    ["methods"] = classMethods,
                });

                // A class that runs once per scenario: its cases per scenario, which the Test layers tab counts per area
                if (FeatureLayerMap.PerScenarioClasses.Contains(shortName))
                {
                    var perScenario = new JsonObject();
                    foreach (var scenario in testClass
                        .Select(m => (Scenario: ScenarioOf(m.Test.Case), m.Outcome, m.Milliseconds))
                        .Where(m => m.Scenario is not null)
                        .GroupBy(m => m.Scenario!, StringComparer.Ordinal)
                        .OrderBy(g => g.Key, StringComparer.Ordinal))
                    {
                        perScenario[scenario.Key] = Tally(scenario.Select(m => (m.Outcome, m.Milliseconds)), []);
                    }

                    node["scenarios"] = perScenario;
                }

                classes.Add(node);
            }

            areas.Add(Tally(area.Select(m => (m.Outcome, m.Milliseconds)), new JsonObject
            {
                ["name"] = area.Key,
                ["covers"] = TestAreaCovers.GetValueOrDefault(area.Key),
                ["classes"] = classes,
            }));
        }

        var finished = DateTimeOffset.TryParse((string?)document.Root?.Element(ns + "Times")?.Attribute("finish"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var finish)
            ? finish
            : (DateTimeOffset?)null;
        return Tally(results.Select(m => (m.Outcome, m.Milliseconds)), new JsonObject
        {
            ["project"] = project,
            ["covers"] = covers,
            ["finished"] = finished?.ToString("O", CultureInfo.InvariantCulture),
            ["stale"] = finished is null || finished < commitTime,
            ["areas"] = areas,
        });
    }
}