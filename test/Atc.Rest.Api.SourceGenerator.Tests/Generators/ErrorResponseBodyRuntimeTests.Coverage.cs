namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The report's code coverage: the Cobertura files the five test projects wrote in the same run
/// (<c>dotnet test --coverage --coverage-output-format cobertura --coverage-output {project}.cobertura.xml</c>, in
/// <c>TestResults/</c>), merged line by line - a line is covered when any suite ran it - for the product assemblies.
/// </summary>
public sealed partial class ErrorResponseBodyRuntimeTests
{
    private static readonly (string Assembly, string What)[] CoveredAssemblies =
    [
        ("Atc.OpenApi", "The OpenAPI extensions and helpers"),
        ("Atc.Rest.Api.Generator", "The extractors, the validator and the services (Roslyn-free)"),
        ("Atc.Rest.Api.SourceGenerator", "The Roslyn source generators"),
        ("Atc.Rest.Api.Generator.Cli", "The CLI and the TypeScript client generator (its commands run as a separate process are not instrumented)"),
        ("Atc.CodeGeneration.CSharp", "The C# code-generation primitives"),
        ("Atc.CodeGeneration.TypeScript", "The TypeScript code-generation primitives"),
    ];

    private static JsonObject? CollectCoverage(string root)
    {
        var folder = Path.Combine(root, "TestResults");
        var files = Directory.Exists(folder)
            ? TestSuiteProjects.Select(p => Path.Combine(folder, p.Project + ".cobertura.xml")).Where(File.Exists).ToList()
            : [];
        if (files.Count == 0)
        {
            return null;
        }

        var commitTime = DateTimeOffset.TryParse(RunTool("git", "log -1 --format=%cI", root).Output.Trim().Split('\n')[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var committed)
            ? committed
            : DateTimeOffset.MaxValue;

        // (assembly, file, line) -> covered, and for a branch line its branches (covered, total)
        var lines = new Dictionary<(string Assembly, string File, int Line), bool>();
        var branches = new Dictionary<(string Assembly, string File, int Line), (int Covered, int Total)>();
        foreach (var file in files)
        {
            var document = System.Xml.Linq.XDocument.Load(file);
            foreach (var package in document.Descendants("package"))
            {
                var assembly = (string?)package.Attribute("name") ?? "?";
                if (!CoveredAssemblies.Any(a => string.Equals(a.Assembly, assembly, StringComparison.Ordinal)))
                {
                    continue;
                }

                foreach (var testClass in package.Descendants("class"))
                {
                    var fileName = ((string?)testClass.Attribute("filename") ?? "?").Replace('\\', '/');
                    var at = fileName.IndexOf("/src/", StringComparison.OrdinalIgnoreCase);
                    fileName = at >= 0 ? fileName[(at + 5)..] : fileName;
                    foreach (var line in testClass.Element("lines")?.Elements("line") ?? [])
                    {
                        var key = (assembly, fileName, (int?)line.Attribute("number") ?? 0);
                        var hit = ((int?)line.Attribute("hits") ?? 0) > 0;
                        lines[key] = lines.GetValueOrDefault(key) || hit;

                        // condition-coverage="50% (1/2)"
                        if (string.Equals((string?)line.Attribute("branch"), "True", StringComparison.OrdinalIgnoreCase) &&
                            System.Text.RegularExpressions.Regex.Match((string?)line.Attribute("condition-coverage") ?? string.Empty, @"\((?<c>\d+)/(?<t>\d+)\)", System.Text.RegularExpressions.RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)) is { Success: true } m)
                        {
                            var covered = int.Parse(m.Groups["c"].Value, CultureInfo.InvariantCulture);
                            var total = int.Parse(m.Groups["t"].Value, CultureInfo.InvariantCulture);
                            var known = branches.GetValueOrDefault(key);
                            branches[key] = (System.Math.Max(known.Covered, covered), System.Math.Max(known.Total, total));
                        }
                    }
                }
            }
        }

        static double Percent(
            int covered,
            int total)
            => total == 0 ? 100 : System.Math.Round(100.0 * covered / total, 1);

        var assemblies = new JsonArray();
        foreach (var (assembly, what) in CoveredAssemblies)
        {
            var own = lines.Where(l => string.Equals(l.Key.Assembly, assembly, StringComparison.Ordinal)).ToList();
            if (own.Count == 0)
            {
                continue;
            }

            var ownBranches = branches.Where(b => string.Equals(b.Key.Assembly, assembly, StringComparison.Ordinal)).ToList();
            var perFile = new JsonArray();
            foreach (var file in own.GroupBy(l => l.Key.File, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var fileBranches = ownBranches.Where(b => string.Equals(b.Key.File, file.Key, StringComparison.Ordinal)).ToList();
                perFile.Add(new JsonObject
                {
                    ["file"] = file.Key,
                    ["lines"] = file.Count(),
                    ["covered"] = file.Count(l => l.Value),
                    ["linePct"] = Percent(file.Count(l => l.Value), file.Count()),
                    ["branchPct"] = Percent(fileBranches.Sum(b => b.Value.Covered), fileBranches.Sum(b => b.Value.Total)),
                });
            }

            assemblies.Add(new JsonObject
            {
                ["assembly"] = assembly,
                ["what"] = what,
                ["lines"] = own.Count,
                ["covered"] = own.Count(l => l.Value),
                ["linePct"] = Percent(own.Count(l => l.Value), own.Count),
                ["branches"] = ownBranches.Sum(b => b.Value.Total),
                ["branchPct"] = Percent(ownBranches.Sum(b => b.Value.Covered), ownBranches.Sum(b => b.Value.Total)),
                ["files"] = perFile,
            });
        }

        var oldest = files.Min(File.GetLastWriteTimeUtc);
        return new JsonObject
        {
            ["suites"] = files.Count,
            ["stale"] = new DateTimeOffset(oldest, TimeSpan.Zero) < commitTime,
            ["linePct"] = Percent(lines.Count(l => l.Value), lines.Count),
            ["branchPct"] = Percent(branches.Sum(b => b.Value.Covered), branches.Sum(b => b.Value.Total)),
            ["assemblies"] = assemblies,
        };
    }
}