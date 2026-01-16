namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Modifies Directory.Build.props files during migration to update target framework and language version.
/// </summary>
internal static class DirectoryBuildPropsModifier
{
    private static readonly Regex TargetFrameworkPattern = new(
        @"<TargetFramework>net\d+\.\d+</TargetFramework>",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly Regex LangVersionPattern = new(
        @"<LangVersion>\d+(?:\.\d+)?</LangVersion>",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Upgrades the target framework and language version in Directory.Build.props.
    /// </summary>
    /// <param name="rootDirectory">The solution root directory.</param>
    /// <param name="targetFramework">The target framework to set (e.g., "net10.0").</param>
    /// <param name="langVersion">The language version to set (e.g., "14").</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>True if the file was modified, false otherwise.</returns>
    public static bool UpgradeTargetFramework(
        string rootDirectory,
        string targetFramework,
        string langVersion,
        bool dryRun = false)
    {
        var propsPath = Path.Combine(rootDirectory, "Directory.Build.props");

        if (!File.Exists(propsPath))
        {
            return false;
        }

        var content = File.ReadAllText(propsPath);
        var modified = false;
        var newContent = content;

        // Update TargetFramework
        if (TargetFrameworkPattern.IsMatch(content))
        {
            newContent = TargetFrameworkPattern.Replace(
                newContent,
                $"<TargetFramework>{targetFramework}</TargetFramework>");
            modified = true;
        }

        // Update LangVersion
        if (LangVersionPattern.IsMatch(newContent))
        {
            newContent = LangVersionPattern.Replace(
                newContent,
                $"<LangVersion>{langVersion}</LangVersion>");
            modified = true;
        }

        if (modified && !dryRun)
        {
            File.WriteAllText(propsPath, newContent);
        }

        return modified;
    }

    /// <summary>
    /// Gets the current target framework from Directory.Build.props.
    /// </summary>
    /// <param name="rootDirectory">The solution root directory.</param>
    /// <returns>The current target framework, or null if not found.</returns>
    public static string? GetCurrentTargetFramework(string rootDirectory)
    {
        var propsPath = Path.Combine(rootDirectory, "Directory.Build.props");

        if (!File.Exists(propsPath))
        {
            return null;
        }

        var content = File.ReadAllText(propsPath);
        var match = TargetFrameworkPattern.Match(content);

        if (match.Success)
        {
            var value = match.Value;
            var start = value.IndexOf('>', StringComparison.Ordinal) + 1;
            var end = value.LastIndexOf('<');
            return value.Substring(start, end - start);
        }

        return null;
    }

    /// <summary>
    /// Upgrades the target framework in all .csproj files that have an explicit TargetFramework element.
    /// </summary>
    /// <param name="rootDirectory">The solution root directory.</param>
    /// <param name="targetFramework">The target framework to set (e.g., "net10.0").</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>A list of csproj files that were modified.</returns>
    public static List<string> UpgradeCsprojTargetFrameworks(
        string rootDirectory,
        string targetFramework,
        bool dryRun = false)
    {
        var modifiedFiles = new List<string>();
        var srcDirectory = Path.Combine(rootDirectory, "src");

        if (!Directory.Exists(srcDirectory))
        {
            return modifiedFiles;
        }

        var csprojFiles = Directory.GetFiles(srcDirectory, "*.csproj", SearchOption.AllDirectories);

        foreach (var csprojFile in csprojFiles)
        {
            var content = File.ReadAllText(csprojFile);

            if (!TargetFrameworkPattern.IsMatch(content))
            {
                continue;
            }

            var newContent = TargetFrameworkPattern.Replace(
                content,
                $"<TargetFramework>{targetFramework}</TargetFramework>");

            if (string.Equals(content, newContent, StringComparison.Ordinal))
            {
                continue;
            }

            if (!dryRun)
            {
                File.WriteAllText(csprojFile, newContent);
            }

            modifiedFiles.Add(csprojFile);
        }

        return modifiedFiles;
    }
}