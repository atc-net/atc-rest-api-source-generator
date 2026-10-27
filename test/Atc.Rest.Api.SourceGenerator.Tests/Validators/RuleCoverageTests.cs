// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// Ensures every rule in RuleIdentifiers has at least one test, and says which: a test class or method marked
/// <c>[RuleTest("ATC_API_...")]</c> in either test project, or a test method of this assembly whose name carries the
/// rule's short code (<c>..._ReportsNAM001</c>). A new rule without a test fails the build.
/// </summary>
public class RuleCoverageTests
{
    /// <summary>
    /// Rules that cannot be unit tested, each with its reason. The catch of each generator step (GEN001, GEN003, GEN005,
    /// GEN008) is tested in Generators.GeneratorFailureDiagnosticsTests; the other GEN ids (a document that does not
    /// parse, no output directory, no endpoints to inject, the summary) are produced by a real generator run in
    /// Generators.GeneratorDiagnosticsTests.
    /// </summary>
    internal static readonly Dictionary<string, string> ExcludedRules = new(StringComparer.Ordinal);

    private static readonly string[] TestProjects = ["Atc.Rest.Api.Generator.Tests", "Atc.Rest.Api.SourceGenerator.Tests"];

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    public void AllRules_ShouldHaveTests()
    {
        // Arrange
        var allRules = GetAllRuleIdentifiers();

        // Act
        var testedRules = GetRuleTests();

        // Assert
        var untestedRules = allRules
            .Except(ExcludedRules.Keys, StringComparer.Ordinal)
            .Where(r => !testedRules.ContainsKey(r))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            untestedRules.Count == 0,
            $"The following rules have no unit tests:\n{string.Join("\n", untestedRules.Select(r => $"  - {r}"))}");
    }

    [Fact]
    public void ExcludedRules_ShouldExistInRuleIdentifiers()
    {
        var invalidExclusions = ExcludedRules.Keys
            .Except(GetAllRuleIdentifiers(), StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            invalidExclusions.Count == 0,
            $"The following excluded rules do not exist in RuleIdentifiers:\n{string.Join("\n", invalidExclusions.Select(r => $"  - {r}"))}");
    }

    /// <summary>
    /// A <c>[RuleTest]</c> names a rule that exists, on a class that mentions it (its short code or its
    /// RuleIdentifiers name) - a typo or a test rewritten for another rule otherwise counts a rule as tested.
    /// </summary>
    [Fact]
    public void RuleTestAttributes_NameARuleTheTestCovers()
    {
        var allRules = GetAllRuleIdentifiers();
        var ruleNames = typeof(Generator.RuleIdentifiers)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.GetValue(null) is string)
            .ToDictionary(f => (string)f.GetValue(null)!, f => f.Name, StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var (ruleId, testClass, source) in FindRuleTestAttributes())
        {
            if (!allRules.Contains(ruleId))
            {
                problems.Add($"{testClass}: [RuleTest(\"{ruleId}\")] names no rule");
                continue;
            }

            var withoutAttributes = source.Replace($"[RuleTest(\"{ruleId}\")]", string.Empty, StringComparison.Ordinal);
            if (!withoutAttributes.Contains(ExtractShortCode(ruleId)!, StringComparison.OrdinalIgnoreCase) &&
                !withoutAttributes.Contains(ruleNames[ruleId], StringComparison.Ordinal))
            {
                problems.Add($"{testClass}: [RuleTest(\"{ruleId}\")] but the class mentions neither {ExtractShortCode(ruleId)} nor {ruleNames[ruleId]}");
            }
        }

        Assert.True(problems.Count == 0, "Rule tests that cover no rule:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void ExcludedRules_ShouldNotBeAccidentallyTested()
    {
        var testedExclusions = ExcludedRules.Keys
            .Where(GetRuleTests().ContainsKey)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            testedExclusions.Count == 0,
            $"The following excluded rules now have tests and should be removed from ExcludedRules:\n{string.Join("\n", testedExclusions.Select(r => $"  - {r}"))}");
    }

    internal static HashSet<string> GetAllRuleIdentifiers()
    {
        var fields = typeof(Generator.RuleIdentifiers).GetFields(
            BindingFlags.Public |
            BindingFlags.Static |
            BindingFlags.FlattenHierarchy);

        var rules = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (field.IsLiteral &&
                !field.IsInitOnly &&
                field.FieldType == typeof(string) &&
                field.GetValue(null) is string value && value.StartsWith("ATC_API_", StringComparison.Ordinal))
            {
                rules.Add(value);
            }
        }

        return rules;
    }

    /// <summary>
    /// Every rule that has a test, with the test classes that cover it (full names): the classes and methods marked
    /// <c>[RuleTest]</c> in either test project, and the test methods of this assembly named after the rule.
    /// </summary>
    internal static Dictionary<string, SortedSet<string>> GetRuleTests()
    {
        var tests = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        void Add(
            string ruleId,
            string testClass)
        {
            if (!tests.TryGetValue(ruleId, out var classes))
            {
                classes = new SortedSet<string>(StringComparer.Ordinal);
                tests[ruleId] = classes;
            }

            classes.Add(testClass);
        }

        foreach (var (ruleId, testClass, _) in FindRuleTestAttributes())
        {
            Add(ruleId, testClass);
        }

        var ruleIds = GetAllRuleIdentifiers();
        foreach (var testClass in typeof(RuleCoverageTests).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t != typeof(RuleCoverageTests)))
        {
            foreach (var method in testClass.GetMethods().Where(m => m.IsDefined(typeof(FactAttribute), inherit: false) || m.IsDefined(typeof(TheoryAttribute), inherit: false)))
            {
                foreach (var ruleId in ruleIds.Where(r => method.Name.Contains(ExtractShortCode(r)!, StringComparison.OrdinalIgnoreCase)))
                {
                    Add(ruleId, testClass.FullName!);
                }
            }
        }

        return tests;
    }

    /// <summary>
    /// The <c>[RuleTest("...")]</c> attributes in the test projects' sources, with the test class each is in (the class
    /// declared after the attribute) and that class's file.
    /// </summary>
    private static IEnumerable<(string RuleId, string TestClass, string Source)> FindRuleTestAttributes()
    {
        var testRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var separator = Path.DirectorySeparatorChar;
        foreach (var project in TestProjects)
        {
            var files = Directory
                .EnumerateFiles(Path.Combine(testRoot, project), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{separator}obj{separator}", StringComparison.Ordinal) && !f.Contains($"{separator}bin{separator}", StringComparison.Ordinal));
            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                if (!source.Contains("[RuleTest(\"", StringComparison.Ordinal))
                {
                    continue;
                }

                var ns = Regex.Match(source, @"(?m)^namespace (?<ns>[\w.]+);", RegexOptions.ExplicitCapture, RegexTimeout).Groups["ns"].Value;
                foreach (Match attribute in Regex.Matches(source, @"\[RuleTest\(""(?<id>ATC_API_\w+)""\)\]", RegexOptions.ExplicitCapture, RegexTimeout))
                {
                    var declaration = Regex.Match(source[attribute.Index..], @"\b(class|void|Task)\s+\w+", RegexOptions.ExplicitCapture, RegexTimeout);
                    var owner = Regex.Matches(source[..(attribute.Index + declaration.Index + declaration.Length)], @"\bclass\s+(?<name>\w+)", RegexOptions.ExplicitCapture, RegexTimeout).LastOrDefault()?.Groups["name"].Value;
                    yield return (attribute.Groups["id"].Value, $"{ns}.{owner}", source);
                }
            }
        }
    }

    private static string? ExtractShortCode(string ruleId)
    {
        // "ATC_API_NAM001" -> "NAM001"
        const string prefix = "ATC_API_";
        return ruleId.StartsWith(prefix, StringComparison.Ordinal)
            ? ruleId[prefix.Length..]
            : null;
    }
}