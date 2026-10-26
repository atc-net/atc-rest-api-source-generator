namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

/// <summary>
/// Helper class for CLI test utilities.
/// </summary>
public static partial class CliTestHelper
{
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
        // From: test/Atc.Rest.Api.Generator.Cli.Tests/bin/{config}/net10.0/
        // To:   src/Atc.Rest.Api.Generator.Cli/bin/{config}/net10.0/atc-rest-api-gen.exe
        var cliPath = Path.GetFullPath(
            Path.Combine(
                testDir,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "Atc.Rest.Api.Generator.Cli",
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

    /// <summary>
    /// Gets the directory of the CLI project's source files (src/Atc.Rest.Api.Generator.Cli).
    /// </summary>
    /// <returns>The absolute path of the CLI source directory.</returns>
    public static string GetCliSourceDirectory()
        => Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "Atc.Rest.Api.Generator.Cli"));

    /// <summary>
    /// Runs the CLI executable and returns its exit code together with the combined stdout/stderr
    /// output, with ANSI color codes removed.
    /// </summary>
    /// <param name="arguments">The command line arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="columns">The <c>COLUMNS</c> the CLI runs with (wide, so a long path stays on one line whatever the
    /// machine's temp directory); null runs it without.</param>
    /// <returns>The exit code and the clean output.</returns>
    public static async Task<(int ExitCode, string Output)> RunCliAsync(
        string arguments,
        CancellationToken cancellationToken,
        string? columns = "1000")
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GetCliExecutableFile().FullName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (columns is null)
        {
            startInfo.Environment.Remove("COLUMNS");
        }
        else
        {
            startInfo.Environment["COLUMNS"] = columns;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The CLI process could not be started.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await stdoutTask + Environment.NewLine + await stderrTask;
        return (process.ExitCode, StripAnsiCodes(output));
    }

    [GeneratedRegex(@"\x1B\[[0-9;]*m", RegexOptions.NonBacktracking)]
    private static partial Regex AnsiCodeRegex();
}