// ReSharper disable InvertIf
// ReSharper disable StringLiteralTypo
using DiagnosticMessage = Atc.Rest.Api.Generator.Models.DiagnosticMessage;
using GeneratorDiagnosticSeverity = Atc.Rest.Api.Generator.Models.DiagnosticSeverity;

namespace Atc.Rest.Api.Generator.IntegrationTests;

/// <summary>
/// Parameterized tests for all OpenAPI scenarios.
/// Auto-discovers scenarios from test/Scenarios/ folder.
/// Uses CodeGenerationService directly without Roslyn dependencies.
/// </summary>
/// <remarks>
/// Uses the folder structure:
/// - Scenario folders: {ScenarioName}/
/// - YAML files: {ScenarioName}/{ScenarioName}.yaml
/// - Config folders: {ScenarioName}/Server/, {ScenarioName}/Client-Typed/, etc.
/// - Output baselines: {ScenarioName}/{MasterFolder}/{Category}/*.verified.cs
/// </remarks>
[Trait("Category", "Integration")]
public class ScenarioTests
{
    /// <summary>
    /// Provides all discovered scenario names for parameterized tests.
    /// </summary>
    public static IEnumerable<object[]> AllScenarios
        => ScenarioDiscovery
            .GetScenarioNames()
            .Select(name => new object[] { name });

    /// <summary>
    /// Provides all scenario/masterFolder/generator combinations.
    /// </summary>
    public static IEnumerable<object[]> AllScenarioGenerators
        => ScenarioDiscovery.BuildScenarioGeneratorData();

    /// <summary>
    /// Tests that the OpenAPI YAML specification passes validation.
    /// Uses the validation strategy configured in the scenario's marker file.
    /// Fails if any validation errors are found, reporting all errors and warnings.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarios))]
    public void YamlValidation_HasNoErrors(string scenarioName)
    {
        // 1. Load OpenAPI document from root YAML path
        var yamlPath = ScenarioDiscovery.GetYamlPath(scenarioName);
        var openApiDoc = GeneratorTestHelper.LoadOpenApiDocument(yamlPath);

        // 2. Get validation strategy from first available marker file
        var strategy = GetValidationStrategy(scenarioName);

        // 3. Run validation (no parsing errors since LoadOpenApiDocument succeeded)
        var diagnostics = OpenApiDocumentValidator.Validate(
            strategy,
            openApiDoc,
            Array.Empty<OpenApiError>(),
            $"{scenarioName}.yaml");

        // 4. Separate errors from warnings
        var errors = diagnostics
            .Where(d => d.Severity == GeneratorDiagnosticSeverity.Error)
            .ToList();
        var warnings = diagnostics
            .Where(d => d.Severity == GeneratorDiagnosticSeverity.Warning)
            .ToList();

        // 5. Fail with report if errors exist
        if (errors.Count > 0)
        {
            var report = FormatValidationReport(scenarioName, errors, warnings);
            Assert.Fail(report);
        }
    }

    /// <summary>
    /// Tests that the generator produces expected output for the given scenario.
    /// Uses CodeGenerationService directly without Roslyn.
    /// Uses Verify for snapshot testing.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllScenarioGenerators))]
    public async Task Generator_GeneratesExpectedTypes(
        string scenarioName,
        string masterFolder,
        string generator)
    {
        var yamlPath = ScenarioDiscovery.GetYamlPath(scenarioName);
        var markerFile = ScenarioDiscovery.GetMarkerFileForGenerator(generator);

        if (markerFile == null)
        {
            Assert.Fail($"Unknown generator type: {generator}");
            return;
        }

        var markerPath = ScenarioDiscovery.GetMarkerPath(scenarioName, masterFolder, markerFile);

        var types = generator.ToUpperInvariant() switch
        {
            "SERVER" => GeneratorTestHelper.GetServerTypesWithPaths(yamlPath, scenarioName).ToList(),
            "CLIENT" => GeneratorTestHelper.GetClientTypesWithPaths(yamlPath, markerPath, scenarioName).ToList(),
            "SERVERDOMAIN" => GeneratorTestHelper.GetServerDomainTypesWithPaths(yamlPath, scenarioName).ToList(),
            "TYPESCRIPTCLIENT" => GeneratorTestHelper.GetTypeScriptClientTypesWithPaths(yamlPath, markerPath, scenarioName).ToList(),
            _ => throw new ArgumentException($"Unknown generator type: {generator}", nameof(generator)),
        };

        var baseDir = GetSourceSnapshotDirectory(scenarioName, masterFolder);
        var isTypeScript = string.Equals(generator, "TypeScriptClient", StringComparison.OrdinalIgnoreCase);

        foreach (var type in types)
        {
            await VerifyGeneratedTypeAsync(type, baseDir, isTypeScript);
        }
    }

    /// <summary>
    /// Verifies a generated type against its verified snapshot file using Verify.
    /// </summary>
    private Task VerifyGeneratedTypeAsync(
        GeneratedType type,
        string baseDir,
        bool isTypeScript = false)
    {
        var directory = Path.Combine(baseDir, type.SubFolder ?? string.Empty);

        // Sanitize type name for Windows filename compatibility
        // Replace <T> with [T] and : with " - " to avoid invalid filename characters
        var safeTypeName = type.TypeName
            .Replace("<", "[", StringComparison.Ordinal)
            .Replace(">", "]", StringComparison.Ordinal)
            .Replace(":", " -", StringComparison.Ordinal);

        string actualContent;
        string extension;

        if (isTypeScript)
        {
            // TypeScript files store their content directly
            actualContent = type.Content;
            extension = "ts";
        }
        else
        {
            var formattedContent = CodeGenerationService.FormatAsTestFile(type);
            actualContent = formattedContent.TrimEnd();
            extension = "cs";
        }

        var settings = new VerifySettings();
        settings.UseDirectory(directory);
        settings.UseFileName(safeTypeName);

        return Verify(actualContent, extension, settings);
    }

    /// <summary>
    /// Gets the absolute path to the snapshot directory for a scenario/masterFolder/generator combination.
    /// Uses the {scenario}/{masterFolder} structure (category subfolders handled by GeneratedType.SubFolder).
    /// </summary>
    private static string GetSourceSnapshotDirectory(
        string scenarioName,
        string masterFolder,
        [CallerFilePath] string sourceFilePath = "")
    {
        // From test/Atc.Rest.Api.Generator.IntegrationTests/ScenarioTests.cs
        // Navigate to test/Scenarios/
        var testProjectDir = Path.GetDirectoryName(sourceFilePath)!;
        var scenariosPath = Path.Combine(testProjectDir, "..", "Scenarios");
        var outputPath = Path.GetFullPath(scenariosPath);
        return Path.Combine(outputPath, scenarioName, masterFolder);
    }

    /// <summary>
    /// Gets the validation strategy from the scenario's server marker file.
    /// Checks Server master folder first.
    /// Defaults to Standard if not specified or file not found.
    /// </summary>
    private static ValidateSpecificationStrategy GetValidationStrategy(
        string scenarioName,
        [CallerFilePath] string sourceFilePath = "")
    {
        // Check Server folder first for validation strategy
        var testProjectDir = Path.GetDirectoryName(sourceFilePath)!;
        var scenariosPath = Path.GetFullPath(Path.Combine(testProjectDir, "..", "Scenarios"));
        var markerPath = Path.Combine(
            scenariosPath,
            scenarioName,
            "Server",
            ".atc-rest-api-server");

        if (!File.Exists(markerPath))
        {
            return ValidateSpecificationStrategy.Standard;
        }

        try
        {
            var json = File.ReadAllText(markerPath);
            var config = JsonSerializer.Deserialize<ServerConfig>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return config?.ValidateSpecificationStrategy ?? ValidateSpecificationStrategy.Standard;
        }
        catch (JsonException)
        {
            return ValidateSpecificationStrategy.Standard;
        }
        catch (IOException)
        {
            return ValidateSpecificationStrategy.Standard;
        }
    }

    /// <summary>
    /// Formats a validation report for test failure output.
    /// </summary>
    private static string FormatValidationReport(
        string scenarioName,
        List<DiagnosticMessage> errors,
        List<DiagnosticMessage> warnings)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append("YAML Validation Failed for scenario '");
        sb.Append(scenarioName);
        sb.AppendLine("'");
        sb.AppendLine();

        if (errors.Count > 0)
        {
            sb.Append("Errors (");
            sb.Append(errors.Count);
            sb.AppendLine("):");
            foreach (var error in errors)
            {
                sb.Append("  [");
                sb.Append(error.RuleId);
                sb.Append("] ");
                sb.AppendLine(error.Message);
            }

            sb.AppendLine();
        }

        if (warnings.Count > 0)
        {
            sb.Append("Warnings (");
            sb.Append(warnings.Count);
            sb.AppendLine("):");
            foreach (var warning in warnings)
            {
                sb.Append("  [");
                sb.Append(warning.RuleId);
                sb.Append("] ");
                sb.AppendLine(warning.Message);
            }
        }

        return sb.ToString();
    }
}