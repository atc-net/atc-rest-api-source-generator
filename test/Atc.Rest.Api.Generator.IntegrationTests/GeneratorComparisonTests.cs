// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.IntegrationTests;

/// <summary>
/// Structure validation tests for generated output.
/// Content comparison is handled by ScenarioTests using Verify.
/// Uses CodeGenerationService directly without Roslyn dependencies.
/// </summary>
/// <remarks>
/// Folder structure:
/// test/Scenarios/
/// ├── Demo/
/// │   ├── Demo.yaml                    (YAML inside scenario folder)
/// │   ├── Server/                      (master folder with marker file)
/// │   │   ├── .atc-rest-api-server-contracts
/// │   │   └── Models/*.verified.cs     (verified files in category subfolders)
/// │   ├── Client-Typed/
/// │   ├── Client-Operation/
/// │   └── ServerDomain/
/// └── PetStoreSimple/
///     └── ...
/// </remarks>
[Trait("Category", "Integration")]
public class GeneratorComparisonTests
{
    /// <summary>
    /// Provides scenario/masterFolder/generator combinations for structure tests.
    /// </summary>
    public static IEnumerable<object[]> GetScenarioMasterGenerators()
        => ScenarioDiscovery.BuildScenarioGeneratorData();

    /// <summary>
    /// Verifies that the generator produces the expected number of files.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetScenarioMasterGenerators))]
    public void Structure_FileCount_Matches(
        string scenario,
        string masterFolder,
        string generator)
    {
        // Arrange
        var expectedFiles = ScenarioDiscovery
            .GetExpectedFiles(scenario, masterFolder, generator)
            .ToList();

        var actualTypes = GetActualTypes(scenario, masterFolder, generator).ToList();

        // Act
        var actualRelativePaths = actualTypes
            .Select(FileComparer.GetRelativePath)
            .ToList();

        // Assert
        if (expectedFiles.Count == actualRelativePaths.Count)
        {
            return;
        }

        var missing = expectedFiles
            .Except(actualRelativePaths, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var extra = actualRelativePaths
            .Except(expectedFiles, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"File count mismatch for {masterFolder}/{scenario}/{generator}");
        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture, $"Expected: {expectedFiles.Count}, Actual: {actualRelativePaths.Count}");
        sb.AppendLine();

        if (missing.Count > 0)
        {
            sb.AppendLine("Missing files:");
            foreach (var file in missing)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  [-] {file}");
                sb.AppendLine();
            }
        }

        if (extra.Count > 0)
        {
            sb.AppendLine("Extra files:");
            foreach (var file in extra)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  [+] {file}");
                sb.AppendLine();
            }
        }

        Assert.Fail(sb.ToString());
    }

    /// <summary>
    /// Verifies that folder structure matches exactly.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetScenarioMasterGenerators))]
    public void Structure_Folders_Match(
        string scenario,
        string masterFolder,
        string generator)
    {
        // Arrange
        var expectedFiles = ScenarioDiscovery
            .GetExpectedFiles(scenario, masterFolder, generator)
            .ToList();

        var actualTypes = GetActualTypes(scenario, masterFolder, generator).ToList();

        // Act - normalize path separators to forward slashes for consistent comparison
        var expectedFolders = expectedFiles
            .Select(f => NormalizePath(Path.GetDirectoryName(f) ?? string.Empty))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(f => !string.IsNullOrEmpty(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var actualFolders = actualTypes
            .Select(t => NormalizePath(t.SubFolder ?? string.Empty))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(f => !string.IsNullOrEmpty(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Assert
        if (expectedFolders.SequenceEqual(actualFolders, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Folder structure mismatch for {masterFolder}/{scenario}/{generator}");
        sb.AppendLine();

        var missingFolders = expectedFolders.Except(actualFolders, StringComparer.OrdinalIgnoreCase).ToList();
        var extraFolders = actualFolders.Except(expectedFolders, StringComparer.OrdinalIgnoreCase).ToList();

        if (missingFolders.Count > 0)
        {
            sb.AppendLine("Missing folders:");
            foreach (var folder in missingFolders)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  [-] {folder}");
                sb.AppendLine();
            }
        }

        if (extraFolders.Count > 0)
        {
            sb.AppendLine("Extra folders:");
            foreach (var folder in extraFolders)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  [+] {folder}");
                sb.AppendLine();
            }
        }

        Assert.Fail(sb.ToString());
    }

    /// <summary>
    /// Gets actual generated types for a scenario/masterFolder/generator combination.
    /// </summary>
    private static IEnumerable<GeneratedType> GetActualTypes(
        string scenario,
        string masterFolder,
        string generator)
    {
        var yamlPath = ScenarioDiscovery.GetYamlPath(scenario);
        var markerFile = ScenarioDiscovery.GetMarkerFileForGenerator(generator);

        if (markerFile == null)
        {
            return [];
        }

        var markerPath = ScenarioDiscovery.GetMarkerPath(scenario, masterFolder, markerFile);

        return generator.ToUpperInvariant() switch
        {
            "SERVER" => GeneratorTestHelper.GetServerTypesWithPaths(yamlPath, scenario),
            "CLIENT" => GeneratorTestHelper.GetClientTypesWithPaths(yamlPath, markerPath, scenario),
            "SERVERDOMAIN" => GeneratorTestHelper.GetServerDomainTypesWithPaths(yamlPath, scenario),
            _ => throw new ArgumentException($"Unknown generator type: {generator}", nameof(generator)),
        };
    }

    /// <summary>
    /// Normalizes path separators to forward slashes for consistent comparison.
    /// </summary>
    private static string NormalizePath(string path)
        => path.Replace('\\', '/');
}