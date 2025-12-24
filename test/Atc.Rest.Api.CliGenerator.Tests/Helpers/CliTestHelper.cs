namespace Atc.Rest.Api.CliGenerator.Tests.Helpers;

/// <summary>
/// Helper class for CLI test utilities.
/// </summary>
public static partial class CliTestHelper
{
    [GeneratedRegex(@"\x1B\[[0-9;]*m", RegexOptions.NonBacktracking)]
    private static partial Regex AnsiCodeRegex();

    /// <summary>
    /// Gets the FileInfo for the CLI executable.
    /// </summary>
    /// <returns>The FileInfo for the CLI executable.</returns>
    public static FileInfo GetCliExecutableFile()
    {
        // Navigate from test output to CLI output
        var testDir = AppDomain.CurrentDomain.BaseDirectory;
        var configuration = testDir.Contains("Release", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";

        // Find the CLI executable path
        // From: test/Atc.Rest.Api.CliGenerator.Tests/bin/{config}/net10.0/
        // To:   src/Atc.Rest.Api.CliGenerator/bin/{config}/net10.0/atc-rest-api-gen.exe
        var cliPath = Path.GetFullPath(
            Path.Combine(
                testDir,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "Atc.Rest.Api.CliGenerator",
                "bin",
                configuration,
                "net10.0",
                "atc-rest-api-gen.exe"));

        if (!File.Exists(cliPath))
        {
            // Try without .exe extension (for cross-platform)
            cliPath = Path.ChangeExtension(cliPath, null);
        }

        return new FileInfo(cliPath);
    }

    /// <summary>
    /// Gets the path to the scenarios folder in the test output directory.
    /// </summary>
    /// <returns>The path to the scenarios folder.</returns>
    public static string GetScenariosPath()
        => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scenarios");

    /// <summary>
    /// Gets the path to a specific scenario YAML file.
    /// </summary>
    /// <param name="scenarioName">The scenario name (without extension).</param>
    /// <returns>The full path to the YAML file.</returns>
    public static string GetScenarioYamlPath(string scenarioName)
        => Path.Combine(GetScenariosPath(), scenarioName, $"{scenarioName}.yaml");

    /// <summary>
    /// Removes ANSI color codes from the output for easier assertion.
    /// </summary>
    /// <param name="text">The text with ANSI codes.</param>
    /// <returns>The text without ANSI codes.</returns>
    public static string StripAnsiCodes(string text)
        => AnsiCodeRegex().Replace(text, string.Empty);
}