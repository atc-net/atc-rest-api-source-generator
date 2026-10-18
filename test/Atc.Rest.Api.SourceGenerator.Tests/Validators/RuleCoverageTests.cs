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
        // ========== Generation Rules (GEN) ==========
        // The catch of each generator step (GEN001, GEN003, GEN005, GEN008) is tested in Generators.GeneratorFailureDiagnosticsTests;
        // the other GEN ids (a document that does not parse, no output directory, no endpoints to inject, the
        // summary) are produced by a real generator run in Generators.GeneratorDiagnosticsTests.
        ["ATC_API_GEN022"] = "MarkerNamespaceRewritten - Tested in Atc.Rest.Api.SourceGenerator.Tests.Generators.RootNamespaceResolutionTests",

        // ========== OpenAPI Validation Rules (VAL) ==========
        ["ATC_API_VAL002"] = "OpenApi20NotSupported - Tested in Atc.Rest.Api.SourceGenerator.Tests.Validators.OpenApiVersionValidationTests",

        // ========== Multi-Part Rules (MPT) ==========
        ["ATC_API_MPT001"] = "DuplicatePathInPart - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT002"] = "DuplicateSchemaInPart - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT003"] = "PartFileContainsProhibitedSection - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT004"] = "MultiPartMergeSuccessful - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT006"] = "UnresolvedReferenceAfterMerge - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT007"] = "PartFileInfoIgnored - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT008"] = "BaseFileNotFound - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT009"] = "DuplicateParameterInPart - Tested in Atc.Rest.Api.Generator.Tests.Services.SpecificationServiceTests",
        ["ATC_API_MPT010"] = "NonIdenticalMergeConflict - Tested in Atc.Rest.Api.Generator.Tests.Services.SpecificationServiceTests",
        ["ATC_API_MPT011"] = "SplitPathSpansGroups - Tested in Atc.Rest.Api.Generator.Tests.Services.SpecificationServiceTests",
        ["ATC_API_MPT012"] = "PartFileOpenApiVersionDiffers - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",
        ["ATC_API_MPT013"] = "PartFileContributesNothing - Tested in Atc.Rest.Api.Generator.Tests.Services.MultiPartDiagnosticsTests",

        // ========== Rules tested in Atc.Rest.Api.Generator.Tests ==========
        // These rules have tests but in a separate test project
        ["ATC_API_OPR010"] = "BadRequestWithoutParameters - Tested in Atc.Rest.Api.Generator.Tests.Validators.ResponseCodeValidationTests",
        ["ATC_API_OPR021"] = "UnauthorizedWithoutSecurity - Tested in Atc.Rest.Api.Generator.Tests.Validators.ResponseCodeValidationTests",
        ["ATC_API_OPR022"] = "ForbiddenWithoutAuthorization - Tested in Atc.Rest.Api.Generator.Tests.Validators.ResponseCodeValidationTests",
        ["ATC_API_OPR023"] = "NotFoundOnPostOperation - Tested in Atc.Rest.Api.Generator.Tests.Validators.ResponseCodeValidationTests",
        ["ATC_API_OPR024"] = "ConflictOnNonMutatingOperation - Tested in Atc.Rest.Api.Generator.Tests.Validators.ResponseCodeValidationTests",
        ["ATC_API_OPR025"] = "TooManyRequestsWithoutRateLimiting - Tested in Atc.Rest.Api.Generator.Tests.Validators.ResponseCodeValidationTests",
        ["ATC_API_CACHE001"] = "OutputCacheOnNonCacheableMethod - Tested in Atc.Rest.Api.Generator.Tests.Validators.OutputCacheValidationTests",
        ["ATC_API_CACHE002"] = "OutputCacheOnAuthenticatedOperation - Tested in Atc.Rest.Api.Generator.Tests.Validators.OutputCacheValidationTests",
        ["ATC_API_OPR027"] = "LargeArrayQueryParameterShouldUseQueryMethod - Tested in Atc.Rest.Api.Generator.Tests.Validators.LargeArrayQueryParameterValidationTests",
        ["ATC_API_OPR028"] = "QueryOperationWithoutRequestBody - Tested in Atc.Rest.Api.Generator.Tests.Validators.QueryOperationShapeValidationTests",
        ["ATC_API_OPR029"] = "QueryOperationWithMutationResponse - Tested in Atc.Rest.Api.Generator.Tests.Validators.QueryOperationShapeValidationTests",

        // ========== Schema Rules (SCH) ==========
        ["ATC_API_SCH022"] = "SchemaWithoutRequiredProperties - Tested in Atc.Rest.Api.Generator.Tests.Validators.SchemaWithoutRequiredValidationTests",
        ["ATC_API_OPR032"] = "OperationIdCollision - Tested in Atc.Rest.Api.Generator.Tests.Validators.OperationIdCollisionValidatorTests",
        ["ATC_API_SEC012"] = "SecuritySchemeNotDeclared - Tested in Atc.Rest.Api.Generator.Tests.Validators.SecuritySchemeReferenceValidatorTests",
        ["ATC_API_SCH024"] = "SchemaTypeOnlyNull - Tested in Atc.Rest.Api.Generator.Tests.Validators.NullOnlyTypeValidatorTests",
        ["ATC_API_OPR033"] = "ParameterWithoutSchema - Tested in Atc.Rest.Api.Generator.Tests.Validators.ParameterSchemaValidatorTests",
        ["ATC_API_OPR034"] = "ResponseStatusRange - Tested in Atc.Rest.Api.Generator.Tests.Validators.ResponseRangeValidatorTests",
        ["ATC_API_OPR037"] = "ParameterValueNotBindable - Tested in Atc.Rest.Api.Generator.Tests.Validators.ParameterBindingValidatorTests",
        ["ATC_API_OPR038"] = "QueryObjectKeyCollision - Tested in Atc.Rest.Api.Generator.Tests.Validators.ParameterBindingValidatorTests",
        ["ATC_API_OPR039"] = "HeadResponseContentIgnored - Tested in Atc.Rest.Api.Generator.Tests.Validators.HeadResponseValidatorTests",
        ["ATC_API_OPR040"] = "MediaTypeDeclaredTwice - Tested in Atc.Rest.Api.Generator.Tests.Validators.MediaTypeDeclaredTwiceValidatorTests",
        ["ATC_API_SCH025"] = "SchemaConstraintNotGeneratable - Tested in Atc.Rest.Api.Generator.Tests.Validators.SchemaConstraintValidatorTests",
        ["ATC_API_SCH026"] = "EnumValueListedTwice - Tested in Atc.Rest.Api.Generator.Tests.Validators.SchemaConstraintValidatorTests",
        ["ATC_API_SCH027"] = "UnsupportedReferenceForm - Tested in Atc.Rest.Api.Generator.Tests.Validators.SchemaReferenceValidatorTests",
        ["ATC_API_VAL003"] = "OpenApiCoreWarning - Tested in Atc.Rest.Api.Generator.Tests.Validators.SchemaReferenceValidatorTests",

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

    /// <summary>
    /// An exclusion that says "Tested in X" names a test class that exists - in the test project its namespace names -
    /// and that mentions the rule (its short code, e.g. MPT001, or its RuleIdentifiers name). A renamed or deleted test
    /// class otherwise leaves the rule silently untested.
    /// </summary>
    [Fact]
    public void ExcludedRules_TestedElsewhere_NameATestClassThatCoversTheRule()
    {
        var testRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var ruleNames = typeof(Generator.RuleIdentifiers)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.GetValue(null) is string)
            .ToDictionary(f => (string)f.GetValue(null)!, f => f.Name, StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var (ruleId, reason) in ExcludedRules.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            var marker = reason.IndexOf("Tested in ", StringComparison.Ordinal);
            if (marker < 0)
            {
                continue;
            }

            var fullName = reason[(marker + "Tested in ".Length)..].Split(' ', ',', ';')[0].TrimEnd('.');
            var lastDot = fullName.LastIndexOf('.');
            var className = fullName[(lastDot + 1)..];
            var classNamespace = fullName[..lastDot];
            var project = new[] { "Atc.Rest.Api.Generator.Tests", "Atc.Rest.Api.SourceGenerator.Tests" }
                .FirstOrDefault(p => classNamespace.StartsWith(p, StringComparison.Ordinal));
            var source = project is null
                ? null
                : Directory
                    .EnumerateFiles(Path.Combine(testRoot, project), "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    .Select(File.ReadAllText)
                    .FirstOrDefault(text => text.Contains($"namespace {classNamespace};", StringComparison.Ordinal) && text.Contains($" class {className}", StringComparison.Ordinal));
            if (source is null)
            {
                problems.Add($"{ruleId}: no class {fullName}");
                continue;
            }

            var shortCode = ExtractShortCode(ruleId);
            if ((shortCode is null || !source.Contains(shortCode, StringComparison.OrdinalIgnoreCase))
                && !source.Contains(ruleNames[ruleId], StringComparison.Ordinal))
            {
                problems.Add($"{ruleId}: {fullName} does not mention {shortCode} or {ruleNames[ruleId]}");
            }
        }

        Assert.True(problems.Count == 0, "Exclusions that point at no covering test:\n" + string.Join("\n", problems));
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