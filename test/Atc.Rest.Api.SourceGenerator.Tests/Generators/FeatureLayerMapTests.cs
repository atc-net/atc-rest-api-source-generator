namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// Every name in <see cref="FeatureLayerMap"/> still points at something: a class selector matches a test class in its
/// project, a scenario folder and a Playwright spec file exist, a matrix key is one the report writes. A rename that
/// leaves the map behind fails here rather than quietly showing an area with fewer tests. (Whether a matrix group has
/// cases is known only when the matrices run: the report checks that.)
/// </summary>
public class FeatureLayerMapTests
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly string TestRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void ClassSelectors_EachMatchATestClassInTheirProject()
    {
        var classes = FeatureLayerMap.Projects.ToDictionary(p => p.Key, p => DeclaredClasses(p.Value), StringComparer.Ordinal);
        var problems = new List<string>();
        foreach (var area in FeatureLayerMap.Areas)
        {
            foreach (var selector in area.Classes)
            {
                var (project, glob) = FeatureLayerMap.Split(selector);
                if (glob is null || !classes.TryGetValue(project, out var declared))
                {
                    problems.Add($"{area.Name}: '{selector}' is not Project:Class (projects: {string.Join(", ", FeatureLayerMap.Projects.Keys)})");
                    continue;
                }

                var matched = declared.Where(c => FeatureLayerMap.Matches(glob, c)).ToList();
                if (matched.Count == 0)
                {
                    problems.Add($"{area.Name}: '{selector}' matches no test class");
                }

                problems.AddRange(matched
                    .Where(FeatureLayerMap.PerScenarioClasses.Contains)
                    .Select(c => $"{area.Name}: '{selector}' names {c}, which runs once per scenario - it counts through the area's scenarios"));
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Scenarios_PlaywrightSpecs_AndMatrixKeys_Exist()
    {
        var scenarios = Path.Combine(TestRoot, "Scenarios");
        var specs = Path.Combine(TestRoot, "Showcase.E2ETests", "specs");
        var matrixSources = string.Concat(Directory
            .EnumerateFiles(Path.Combine(TestRoot, "Atc.Rest.Api.SourceGenerator.Tests", "Generators"), "ErrorResponseBodyRuntimeTests*.cs")
            .Select(File.ReadAllText));

        var problems = new List<string>();
        foreach (var area in FeatureLayerMap.Areas)
        {
            problems.AddRange(area.Scenarios
                .Where(s => !Directory.Exists(Path.Combine(scenarios, s)))
                .Select(s => $"{area.Name}: no scenario folder test/Scenarios/{s}"));
            problems.AddRange(area.Playwright
                .Where(s => !File.Exists(Path.Combine(specs, s)))
                .Select(s => $"{area.Name}: no Playwright spec test/Showcase.E2ETests/specs/{s}"));
            problems.AddRange(area.Matrices
                .Select(m => FeatureLayerMap.Split(m).Head)
                .Where(key => !matrixSources.Contains($"\"{key}\"", StringComparison.Ordinal))
                .Select(key => $"{area.Name}: '{key}' is not a matrix the report writes"));
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Areas_HaveUniqueNames_AndSomethingAtEveryArea()
    {
        Assert.Equal(FeatureLayerMap.Areas.Count, FeatureLayerMap.Areas.Select(a => a.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(FeatureLayerMap.Areas, a => Assert.NotEmpty(a.Classes));
    }

    [Theory]
    [InlineData("SchemaExtractorTests", "SchemaExtractorTests", true)]
    [InlineData("SchemaExtractor*", "SchemaExtractorJsonPropertyNameTests", true)]
    [InlineData("*ValidatorTests", "PathTemplateValidatorTests", true)]
    [InlineData("TypeScript*ExtractorTests", "TypeScriptEnumExtractorTests", true)]
    [InlineData("TypeScript*ExtractorTests", "TypeScriptXmlGateTests", false)]
    [InlineData("SchemaExtractorTests", "SchemaExtractorTestsMore", false)]
    [InlineData("A*A", "A", false)]
    public void Matches_ReadsTheWildcard(
        string glob,
        string className,
        bool expected)
        => Assert.Equal(expected, FeatureLayerMap.Matches(glob, className));

    private static HashSet<string> DeclaredClasses(string project)
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory
            .EnumerateFiles(Path.Combine(TestRoot, project), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separator}obj{separator}", StringComparison.Ordinal) && !f.Contains($"{separator}bin{separator}", StringComparison.Ordinal))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\bclass\s+(?<name>\w+Tests)\b", RegexOptions.ExplicitCapture, RegexTimeout).Select(m => m.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);
    }
}