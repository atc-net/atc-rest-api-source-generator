// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// Ensures every rule in RuleIdentifiers has at least one test.
/// This test fails the build if a new rule is added without corresponding tests.
/// </summary>
public class RuleCoverageTests
{
    /// <summary>
    /// Rules that cannot be unit tested (with documented reasons).
    /// Each entry explains why the rule requires integration testing or is not yet implemented.
    /// </summary>
    private static readonly Dictionary<string, string> ExcludedRules = new(StringComparer.Ordinal)
    {
        // ========== Generation Rules (GEN) - Runtime errors ==========
        ["ATC_API_GEN001"] = "ServerGenerationError - Reported during code generation, requires integration test",
        ["ATC_API_GEN002"] = "ServerParsingError - Reported during parsing, requires integration test",
        ["ATC_API_GEN003"] = "ClientGenerationError - Reported during client generation, requires integration test",
        ["ATC_API_GEN004"] = "ClientParsingError - Reported during parsing, requires integration test",
        ["ATC_API_GEN005"] = "HandlerScaffoldGenerationError - Reported during scaffold generation, requires integration test",
        ["ATC_API_GEN006"] = "DomainParsingError - Reported during parsing, requires integration test",
        ["ATC_API_GEN007"] = "OutputDirectoryNotSpecified - Configuration warning, requires integration test",
        ["ATC_API_GEN008"] = "EndpointInjectionGenerationError - Reported during injection, requires integration test",
        ["ATC_API_GEN009"] = "NoEndpointsFoundForInjection - Runtime warning, requires integration test",
        ["ATC_API_GEN010"] = "GenerationSummary - Info message, requires integration test",

        // ========== Dependency Rules (DEP) - Package reference validation ==========
        ["ATC_API_DEP001"] = "ServerRequiresAspNetCore - Requires project with missing package reference",
        ["ATC_API_DEP002"] = "DomainRequiresAspNetCore - Requires project with missing package reference",
        ["ATC_API_DEP003"] = "ClientRequiresAtcRestClient - Requires project with missing package reference",
        ["ATC_API_DEP004"] = "RateLimitingRequiresPackage - Requires project with missing package reference",
        ["ATC_API_DEP005"] = "ResilienceRequiresPackage - Requires project with missing package reference",
        ["ATC_API_DEP006"] = "JwtBearerRequiresPackage - Requires project with missing package reference",
        ["ATC_API_DEP007"] = "MinimalApiPackageRequired - Requires project with missing package reference",

        // ========== OpenAPI Validation Rules (VAL) - Parsing errors ==========
        ["ATC_API_VAL001"] = "OpenApiCoreError - Reported by Microsoft.OpenApi parser, requires malformed YAML",
        ["ATC_API_VAL002"] = "OpenApi20NotSupported - Checks Info.Version field (API version), not spec version",

        // ========== Multi-Part Rules (MPT) - Requires integration tests ==========
        ["ATC_API_MPT001"] = "DuplicatePathInPart - Requires multi-file parsing, integration test needed",
        ["ATC_API_MPT002"] = "DuplicateSchemaInPart - Requires multi-file parsing, integration test needed",
        ["ATC_API_MPT003"] = "PartFileContainsProhibitedSection - Requires multi-file parsing, integration test needed",
        ["ATC_API_MPT004"] = "MultiPartMergeSuccessful - Info message for multi-file merge, integration test needed",
        ["ATC_API_MPT005"] = "PartFileNotFound - Requires file system access, integration test needed",
        ["ATC_API_MPT006"] = "UnresolvedReferenceAfterMerge - Requires multi-file parsing, integration test needed",
        ["ATC_API_MPT007"] = "PartFileMissingOpenApiVersion - Requires multi-file parsing, integration test needed",
        ["ATC_API_MPT008"] = "BaseFileNotFound - Requires file system access, integration test needed",

        // ========== Rules tested in Atc.Rest.Api.Generator.Tests ==========
        // These rules have tests but in a separate test project
        ["ATC_API_OPR010"] = "BadRequestWithoutParameters - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
        ["ATC_API_OPR021"] = "UnauthorizedWithoutSecurity - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
        ["ATC_API_OPR022"] = "ForbiddenWithoutAuthorization - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
        ["ATC_API_OPR023"] = "NotFoundOnPostOperation - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
        ["ATC_API_OPR024"] = "ConflictOnNonMutatingOperation - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
        ["ATC_API_OPR025"] = "TooManyRequestsWithoutRateLimiting - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
    };

    [Fact]
    public void AllRules_ShouldHaveTests()
    {
        // Arrange: Get all rule IDs from RuleIdentifiers via reflection
        var allRules = GetAllRuleIdentifiers();

        // Act: Find which rules are tested by scanning test assemblies
        var testedRules = GetTestedRules();

        // Assert: Check that all non-excluded rules have tests
        var untestedRules = allRules
            .Except(ExcludedRules.Keys, StringComparer.Ordinal)
            .Except(testedRules, StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            untestedRules.Count == 0,
            $"The following rules have no unit tests:\n" +
            $"{string.Join("\n", untestedRules.Select(r => $"  - {r}"))}");
    }

    [Fact]
    public void ExcludedRules_ShouldExistInRuleIdentifiers()
    {
        // Arrange: Get all rule IDs from RuleIdentifiers
        var allRules = GetAllRuleIdentifiers();

        // Act: Find excluded rules that don't exist in RuleIdentifiers
        var invalidExclusions = ExcludedRules.Keys
            .Except(allRules, StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        // Assert: All excluded rules should exist
        Assert.True(
            invalidExclusions.Count == 0,
            $"The following excluded rules do not exist in RuleIdentifiers:\n" +
            $"{string.Join("\n", invalidExclusions.Select(r => $"  - {r}"))}");
    }

    [Fact]
    public void ExcludedRules_ShouldNotBeAccidentallyTested()
    {
        // Arrange: Get tested rules (in this assembly only)
        var testedRules = GetTestedRules();

        // Act: Find excluded rules that actually have tests in this assembly
        // (We skip rules marked as "Tested in Atc.Rest.Api.Generator.Tests")
        var testedExclusions = ExcludedRules
            .Where(kvp => !kvp.Value.Contains("Tested in Atc.Rest.Api.Generator.Tests", StringComparison.Ordinal))
            .Select(kvp => kvp.Key)
            .Intersect(testedRules, StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        // Assert: Excluded rules should not have tests (otherwise remove from exclusion list)
        Assert.True(
            testedExclusions.Count == 0,
            $"The following excluded rules now have tests and should be removed from ExcludedRules:\n" +
            $"{string.Join("\n", testedExclusions.Select(r => $"  - {r}"))}");
    }

    private static HashSet<string> GetAllRuleIdentifiers()
    {
        var ruleIdentifiersType = typeof(Generator.RuleIdentifiers);
        var fields = ruleIdentifiersType.GetFields(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.FlattenHierarchy);

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

    private static HashSet<string> GetTestedRules()
    {
        var testedRules = new HashSet<string>(StringComparer.Ordinal);

        // Get all rule ID values for lookup
        var ruleIdValues = GetAllRuleIdentifiers();

        // Only scan this test assembly
        var assembly = typeof(RuleCoverageTests).Assembly;

        var testClasses = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetMethods().Any(m =>
                m.GetCustomAttributes(typeof(FactAttribute), inherit: false).Length != 0 ||
                m.GetCustomAttributes(typeof(TheoryAttribute), inherit: false).Length != 0));

        foreach (var testClass in testClasses)
        {
            // Skip this test class to avoid circular reference
            if (testClass == typeof(RuleCoverageTests))
            {
                continue;
            }

            var testMethods = testClass.GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(FactAttribute), inherit: false).Length != 0 ||
                            m.GetCustomAttributes(typeof(TheoryAttribute), inherit: false).Length != 0);

            foreach (var method in testMethods)
            {
                // Check method name for rule references (e.g., "Validate_..._ReportsNAM001")
                foreach (var ruleId in ruleIdValues)
                {
                    // Extract the short code from ATC_API_XXX### format
                    var shortCode = ExtractShortCode(ruleId);
                    if (shortCode is not null && method.Name.Contains(shortCode, StringComparison.OrdinalIgnoreCase))
                    {
                        testedRules.Add(ruleId);
                    }
                }
            }
        }

        return testedRules;
    }

    private static string? ExtractShortCode(string ruleId)
    {
        // Convert "ATC_API_NAM001" to "NAM001"
        const string prefix = "ATC_API_";
        return ruleId.StartsWith(prefix, StringComparison.Ordinal)
            ? ruleId[prefix.Length..]
            : null;
    }
}