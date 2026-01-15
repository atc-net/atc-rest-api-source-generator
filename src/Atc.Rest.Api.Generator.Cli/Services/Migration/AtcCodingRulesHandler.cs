namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Handles ATC coding rules updater configuration during migration.
/// </summary>
internal static class AtcCodingRulesHandler
{
    private const string ConfigFileName = "atc-coding-rules-updater.json";

    private static readonly Regex ProjectTargetPattern = new(
        @"""projectTarget""\s*:\s*""(?<target>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Checks if the ATC coding rules updater configuration exists.
    /// </summary>
    /// <param name="rootDirectory">The root directory of the solution.</param>
    /// <returns>The result of the check.</returns>
    public static AtcCodingRulesResult Check(string rootDirectory)
    {
        var result = new AtcCodingRulesResult();

        // Look for atc-coding-rules-updater.json or similar files
        var configPath = Path.Combine(rootDirectory, ConfigFileName);
        if (File.Exists(configPath))
        {
            result.ConfigExists = true;
            result.ConfigPath = configPath;

            try
            {
                var content = File.ReadAllText(configPath);
                var match = ProjectTargetPattern.Match(content);
                if (match.Success)
                {
                    result.CurrentProjectTarget = match.Groups["target"].Value;
                }
            }
            catch
            {
                // Ignore errors reading the file
            }
        }
        else
        {
            // Check for other patterns
            var patterns = new[] { "atc-coding-rules-updater.*.json", "*.atc-coding-rules*.json" };
            foreach (var pattern in patterns)
            {
                try
                {
                    var files = Directory.GetFiles(rootDirectory, pattern);
                    if (files.Length > 0)
                    {
                        result.ConfigExists = true;
                        result.ConfigPath = files[0];
                        break;
                    }
                }
                catch
                {
                    // Ignore errors
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Updates the project target in the configuration file.
    /// </summary>
    /// <param name="configPath">Path to the configuration file.</param>
    /// <param name="newTarget">The new project target (e.g., "DotNet10").</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>True if the file was modified.</returns>
    public static bool UpdateProjectTarget(
        string configPath,
        string newTarget,
        bool dryRun = false)
    {
        if (!File.Exists(configPath))
        {
            return false;
        }

        var content = File.ReadAllText(configPath);
        var match = ProjectTargetPattern.Match(content);

        if (!match.Success)
        {
            return false;
        }

        var currentTarget = match.Groups["target"].Value;
        if (currentTarget.Equals(newTarget, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var modified = ProjectTargetPattern.Replace(content, $@"""projectTarget"": ""{newTarget}""");

        if (!dryRun)
        {
            File.WriteAllText(configPath, modified);
        }

        return true;
    }

    /// <summary>
    /// Creates a new ATC coding rules updater configuration file.
    /// </summary>
    /// <param name="rootDirectory">The root directory of the solution.</param>
    /// <param name="projectTarget">The project target (e.g., "DotNet10").</param>
    /// <param name="dryRun">If true, only returns what would be created.</param>
    /// <returns>The path to the created file.</returns>
    public static string CreateConfig(
        string rootDirectory,
        string projectTarget,
        bool dryRun = false)
    {
        var configPath = Path.Combine(rootDirectory, ConfigFileName);

        var config = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["projectTarget"] = projectTarget,
            ["useLatestMinorNugetVersion"] = true,
            ["mappings"] = new[]
            {
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["src"] = "src",
                },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["test"] = "test",
                },
            },
        };

        if (!dryRun)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(configPath, json);
        }

        return configPath;
    }

    /// <summary>
    /// Prompts the user to set up ATC coding rules updater.
    /// </summary>
    /// <returns>True if the user wants to set it up.</returns>
    public static bool PromptForSetup()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]![/] ATC Coding Rules Updater not configured");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("We recommend setting up atc-coding-rules-updater to ensure consistent");
        AnsiConsole.MarkupLine("coding standards and analyzer configurations across your project.");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Benefits:[/]");
        AnsiConsole.MarkupLine("  • Consistent .editorconfig and analyzer settings");
        AnsiConsole.MarkupLine("  • Automatic updates when ATC rules change");
        AnsiConsole.MarkupLine("  • Pre-configured for .NET 10 / C# 14");
        AnsiConsole.WriteLine();

        return AnsiConsole.Confirm("Do you want to set up atc-coding-rules-updater now?", defaultValue: true);
    }
}