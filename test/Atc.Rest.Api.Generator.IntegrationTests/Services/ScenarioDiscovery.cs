// ReSharper disable LoopCanBeConvertedToQuery
namespace Atc.Rest.Api.Generator.IntegrationTests.Services;

/// <summary>
/// Discovers test scenarios from the folder structure.
/// YAML files are inside scenario folders, config folders in master folders within scenarios.
/// </summary>
/// <remarks>
/// Structure:
/// test/Scenarios/
/// ├── Demo/
/// │   ├── Demo.yaml                    (YAML inside scenario folder)
/// │   ├── Server/                      (master folder with marker file)
/// │   │   ├── .atc-rest-api-server
/// │   │   └── Models/*.verified.cs     (verified files in category subfolders)
/// │   ├── Client-Typed/
/// │   ├── Client-Operation/
/// │   └── ServerDomain/
/// └── PetStoreSimple/
///     └── ...
/// </remarks>
public static class ScenarioDiscovery
{
    /// <summary>
    /// Marker file to generator type and output folder mapping.
    /// </summary>
    private static readonly Dictionary<string, (string GeneratorType, string OutputFolder)> MarkerFileMappingInternal = new(StringComparer.Ordinal)
    {
        [".atc-rest-api-server"] = ("Server", "Server"),
        [".atc-rest-api-server-handlers"] = ("ServerDomain", "ServerDomain"),
        [".atc-rest-api-client"] = ("Client", "Client"),
    };

    /// <summary>
    /// Marker file to generator type and output folder mapping.
    /// </summary>
    public static IReadOnlyDictionary<string, (string GeneratorType, string OutputFolder)> MarkerFileMapping
        => MarkerFileMappingInternal;

    /// <summary>
    /// Known master folders for different generator configurations.
    /// </summary>
    public static readonly string[] MasterFolders = ["Server", "Client-Typed", "Client-Operation", "ServerDomain"];

    /// <summary>
    /// Valid generator types for comparison.
    /// </summary>
    public static readonly string[] GeneratorTypes = ["Server", "Client", "ServerDomain"];

    /// <summary>
    /// Gets the base path for test scenarios (test/Scenarios/).
    /// </summary>
    public static string GetScenariosBasePath()
        => GeneratorTestHelper.GetScenariosBasePath();

    /// <summary>
    /// Gets all scenario folder names that contain a YAML file.
    /// Returns folder names (e.g., "Demo", "PetStoreSimple").
    /// </summary>
    public static IEnumerable<string> GetScenarioNames()
    {
        var basePath = GetScenariosBasePath();

        if (!Directory.Exists(basePath))
        {
            yield break;
        }

        foreach (var scenarioDir in Directory.GetDirectories(basePath))
        {
            var scenarioName = Path.GetFileName(scenarioDir);
            var yamlPath = Path.Combine(scenarioDir, $"{scenarioName}.yaml");
            if (File.Exists(yamlPath))
            {
                yield return scenarioName;
            }
        }
    }

    /// <summary>
    /// Gets the full path to a scenario's YAML file.
    /// YAML files are located at {basePath}/{scenarioName}/{scenarioName}.yaml
    /// </summary>
    public static string GetYamlPath(string scenarioName)
        => Path.Combine(GetScenariosBasePath(), scenarioName, $"{scenarioName}.yaml");

    /// <summary>
    /// Gets all master folders that exist within the given scenario folder.
    /// Master folders are located at {basePath}/{scenarioName}/{masterFolder}
    /// </summary>
    public static IEnumerable<string> GetMasterFoldersForScenario(
        string scenarioName)
    {
        var basePath = GetScenariosBasePath();

        foreach (var masterFolder in MasterFolders)
        {
            var configPath = Path.Combine(basePath, scenarioName, masterFolder);
            if (Directory.Exists(configPath))
            {
                yield return masterFolder;
            }
        }
    }

    /// <summary>
    /// Gets the path to the config folder for a scenario/master folder combination.
    /// Config folders are located at {basePath}/{scenarioName}/{masterFolder}
    /// </summary>
    public static string GetConfigPath(
        string scenarioName,
        string masterFolder)
        => Path.Combine(GetScenariosBasePath(), scenarioName, masterFolder);

    /// <summary>
    /// Gets the path to a marker file in the config folder.
    /// </summary>
    public static string GetMarkerPath(
        string scenarioName,
        string masterFolder,
        string markerFile)
        => Path.Combine(GetConfigPath(scenarioName, masterFolder), markerFile);

    /// <summary>
    /// Gets all generator types available for a scenario/master folder combination.
    /// Detects generators based on marker files present in the config folder.
    /// </summary>
    public static IEnumerable<string> GetGeneratorsForScenario(
        string scenarioName,
        string masterFolder)
    {
        var configPath = GetConfigPath(scenarioName, masterFolder);

        if (!Directory.Exists(configPath))
        {
            yield break;
        }

        foreach (var (markerFile, (generatorType, _)) in MarkerFileMapping)
        {
            var markerPath = Path.Combine(configPath, markerFile);
            if (File.Exists(markerPath))
            {
                yield return generatorType;
            }
        }
    }

    /// <summary>
    /// Gets the marker file name for a generator type.
    /// </summary>
    public static string? GetMarkerFileForGenerator(string generator)
    {
        foreach (var (markerFile, (generatorType, _)) in MarkerFileMapping)
        {
            if (string.Equals(generatorType, generator, StringComparison.OrdinalIgnoreCase))
            {
                return markerFile;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the baseline output path for a specific scenario/master/generator.
    /// Path format: {Scenario}/{MasterFolder}/ (category subfolders handled by GeneratedType.SubFolder)
    /// </summary>
    public static string GetBaselinePath(
        string scenarioName,
        string masterFolder,
        string generator)
        => Path.Combine(GetScenariosBasePath(), scenarioName, masterFolder);

    /// <summary>
    /// Gets all expected baseline files for a scenario/master/generator combination.
    /// Returns paths relative to the generator output folder.
    /// </summary>
    public static IEnumerable<string> GetExpectedFiles(
        string scenarioName,
        string masterFolder,
        string generator)
    {
        var baselinePath = GetBaselinePath(scenarioName, masterFolder, generator);

        if (!Directory.Exists(baselinePath))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(baselinePath, "*.verified.cs", SearchOption.AllDirectories))
        {
            yield return Path.GetRelativePath(baselinePath, file);
        }
    }

    /// <summary>
    /// Gets the full path to an expected file.
    /// </summary>
    public static string GetExpectedFilePath(
        string scenarioName,
        string masterFolder,
        string generator,
        string relativePath)
        => Path.Combine(GetBaselinePath(scenarioName, masterFolder, generator), relativePath);

    /// <summary>
    /// Reads the content of an expected file.
    /// </summary>
    public static string ReadExpectedFile(
        string scenarioName,
        string masterFolder,
        string generator,
        string relativePath)
    {
        var path = GetExpectedFilePath(scenarioName, masterFolder, generator, relativePath);
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Reads and parses a marker file configuration.
    /// Returns the parsed config or null if parsing fails.
    /// </summary>
    /// <typeparam name="T">The configuration type to deserialize to.</typeparam>
    /// <param name="markerPath">The path to the marker file.</param>
    /// <returns>The parsed configuration or null if parsing fails.</returns>
    public static T? ReadMarkerConfig<T>(string markerPath)
        where T : class
    {
        if (!File.Exists(markerPath))
        {
            return null;
        }

        var content = File.ReadAllText(markerPath);
        if (string.IsNullOrWhiteSpace(content) || content.Trim() == "{}")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(content);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds test data for Theory tests with all scenario/masterFolder/generator combinations.
    /// Returns: [scenario, masterFolder, generator]
    /// </summary>
    public static IEnumerable<object[]> BuildScenarioGeneratorData()
    {
        foreach (var scenario in GetScenarioNames())
        {
            foreach (var masterFolder in GetMasterFoldersForScenario(scenario))
            {
                foreach (var generator in GetGeneratorsForScenario(scenario, masterFolder))
                {
                    yield return [scenario, masterFolder, generator];
                }
            }
        }
    }

    /// <summary>
    /// Builds test data for Theory tests with all individual files.
    /// Returns: [scenario, masterFolder, generator, relativePath]
    /// </summary>
    public static IEnumerable<object[]> BuildFileComparisonData()
    {
        foreach (var scenario in GetScenarioNames())
        {
            foreach (var masterFolder in GetMasterFoldersForScenario(scenario))
            {
                foreach (var generator in GetGeneratorsForScenario(scenario, masterFolder))
                {
                    foreach (var file in GetExpectedFiles(scenario, masterFolder, generator))
                    {
                        yield return [scenario, masterFolder, generator, file];
                    }
                }
            }
        }
    }
}