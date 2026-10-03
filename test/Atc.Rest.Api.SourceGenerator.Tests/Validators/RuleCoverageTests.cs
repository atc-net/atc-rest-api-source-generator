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
    internal static readonly Dictionary<string, string> ExcludedRules = new(StringComparer.Ordinal)
    {
        // ========== Generation Rules (GEN) - the catch-all of a generator run ==========
        // The other GEN ids (a document that does not parse, no output directory, no endpoints to inject, the
        // summary) are produced by a real generator run in Generators.GeneratorDiagnosticsTests.
        ["ATC_API_GEN001"] = "ServerGenerationError - the catch around server generation; no known input makes the generator throw",
        ["ATC_API_GEN003"] = "ClientGenerationError - the catch around client generation; no known input makes the generator throw",
        ["ATC_API_GEN005"] = "HandlerScaffoldGenerationError - the catch around scaffold generation; no known input makes the generator throw",
        ["ATC_API_GEN008"] = "EndpointInjectionGenerationError - the catch around endpoint injection; no known input makes the generator throw",

        // ========== OpenAPI Validation Rules (VAL) ==========
        ["ATC_API_VAL002"] = "OpenApi20NotSupported - Tested in Atc.Rest.Api.SourceGenerator.Tests.OpenApiVersionValidationTests",

        // ========== Multi-Part Rules (MPT) ==========
        ["ATC_API_MPT001"] = "DuplicatePathInPart - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT002"] = "DuplicateSchemaInPart - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT003"] = "PartFileContainsProhibitedSection - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT004"] = "MultiPartMergeSuccessful - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT006"] = "UnresolvedReferenceAfterMerge - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT007"] = "PartFileInfoIgnored - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT008"] = "BaseFileNotFound - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT009"] = "DuplicateParameterInPart - Tested in Atc.Rest.Api.Generator.Tests.SpecificationServiceTests",
        ["ATC_API_MPT010"] = "NonIdenticalMergeConflict - Tested in Atc.Rest.Api.Generator.Tests.SpecificationServiceTests",
        ["ATC_API_MPT011"] = "SplitPathSpansGroups - Tested in Atc.Rest.Api.Generator.Tests.SpecificationServiceTests",

        // ========== Rules tested in Atc.Rest.Api.Generator.Tests ==========
        // These rules have tests but in a separate test project
        ["ATC_API_OPR010"] = "BadRequestWithoutParameters - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
        ["ATC_API_OPR021"] = "UnauthorizedWithoutSecurity - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
        ["ATC_API_OPR022"] = "ForbiddenWithoutAuthorization - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
        ["ATC_API_OPR023"] = "NotFoundOnPostOperation - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
        ["ATC_API_OPR024"] = "ConflictOnNonMutatingOperation - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
        ["ATC_API_OPR025"] = "TooManyRequestsWithoutRateLimiting - Tested in Atc.Rest.Api.Generator.Tests.ResponseCodeValidationTests",
        ["ATC_API_CACHE001"] = "OutputCacheOnNonCacheableMethod - Tested in Atc.Rest.Api.Generator.Tests.Validators.OutputCacheValidationTests",
        ["ATC_API_OPR027"] = "LargeArrayQueryParameterShouldUseQueryMethod - Tested in Atc.Rest.Api.Generator.Tests.Validators.LargeArrayQueryParameterValidationTests",
        ["ATC_API_OPR028"] = "QueryOperationWithoutRequestBody - Tested in Atc.Rest.Api.Generator.Tests.Validators.QueryOperationShapeValidationTests",
        ["ATC_API_OPR029"] = "QueryOperationWithMutationResponse - Tested in Atc.Rest.Api.Generator.Tests.Validators.QueryOperationShapeValidationTests",

        // ========== Schema Rules (SCH) ==========
        ["ATC_API_SCH022"] = "SchemaWithoutRequiredProperties - Tested in Atc.Rest.Api.Generator.Tests.Validators.SchemaWithoutRequiredValidationTests",

        // ========== Client Rules (CLT) ==========
        ["ATC_API_CLT001"] = "SingleClientTypeNameCollision - Tested in Atc.Rest.Api.Generator.Tests.Validators.SingleClientCollisionValidatorTests",

        // ========== Spec Version Migration Rules (VER) ==========
        ["ATC_API_VER001"] = "NullableWithoutSiblingType - Tested in Atc.Rest.Api.Generator.Tests.Validators.SpecVersionMigrationValidationTests",
        ["ATC_API_VER002"] = "NullableKeywordRemovedInSpecVersion - Tested in Atc.Rest.Api.Generator.Tests.Validators.SpecVersionMigrationValidationTests",
        ["ATC_API_VER003"] = "ExclusiveBoundBooleanForm - Tested in Atc.Rest.Api.Generator.Tests.Validators.SpecVersionMigrationValidationTests",
        ["ATC_API_VER004"] = "TypeArrayNotSupportedInSpecVersion - Tested in Atc.Rest.Api.Generator.Tests.Validators.SpecVersionMigrationValidationTests",
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

    internal static HashSet<string> GetAllRuleIdentifiers()
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

    internal static HashSet<string> GetTestedRules()
    {
        var testedRules = new HashSet<string>(StringComparer.Ordinal);

        // Get all rule ID values for lookup
        var ruleIdValues = GetAllRuleIdentifiers();

        // Only scan this test assembly
        var assembly = typeof(RuleCoverageTests).Assembly;

        var testClasses = assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t
                .GetMethods()
                .Any(m => m.GetCustomAttributes(typeof(FactAttribute), inherit: false).Length != 0 ||
                          m.GetCustomAttributes(typeof(TheoryAttribute), inherit: false).Length != 0));

        foreach (var testClass in testClasses)
        {
            // Skip this test class to avoid circular reference
            if (testClass == typeof(RuleCoverageTests))
            {
                continue;
            }

            var testMethods = testClass
                .GetMethods()
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