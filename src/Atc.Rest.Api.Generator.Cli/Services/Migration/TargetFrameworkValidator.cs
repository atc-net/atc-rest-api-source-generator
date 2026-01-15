namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Validates target framework and language version compatibility for migration.
/// </summary>
internal static class TargetFrameworkValidator
{
    private static readonly Regex TargetFrameworkPattern = new(
        @"<TargetFramework>([^<]+)</TargetFramework>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LangVersionPattern = new(
        @"<LangVersion>([^<]+)</LangVersion>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const decimal MinimumSupportedFramework = 8.0m;
    private const decimal RequiredFramework = 10.0m;
    private const int MinimumSupportedLangVersion = 12;
    private const int RequiredLangVersion = 14;

    /// <summary>
    /// Validates target framework and language version in the project.
    /// </summary>
    /// <param name="rootDirectory">The root directory of the solution.</param>
    /// <param name="projectFiles">List of project files to check.</param>
    /// <returns>The target framework validation result.</returns>
    public static TargetFrameworkResult Validate(string rootDirectory, IReadOnlyList<string> projectFiles)
    {
        var result = new TargetFrameworkResult();

        // First check Directory.Build.props
        var directoryBuildProps = Path.Combine(rootDirectory, "Directory.Build.props");
        if (File.Exists(directoryBuildProps))
        {
            ExtractFromFile(directoryBuildProps, result, "Directory.Build.props");
        }

        // If not found in Directory.Build.props, check project files
        if (string.IsNullOrEmpty(result.CurrentTargetFramework))
        {
            foreach (var projectFile in projectFiles)
            {
                ExtractFromFile(projectFile, result, Path.GetFileName(projectFile));
                if (!string.IsNullOrEmpty(result.CurrentTargetFramework))
                {
                    break;
                }
            }
        }

        // Validate compatibility
        ValidateFrameworkCompatibility(result);
        ValidateLangVersionCompatibility(result);

        return result;
    }

    private static void ExtractFromFile(string filePath, TargetFrameworkResult result, string sourceName)
    {
        try
        {
            var content = File.ReadAllText(filePath);

            // Extract TargetFramework
            if (string.IsNullOrEmpty(result.CurrentTargetFramework))
            {
                var tfmMatch = TargetFrameworkPattern.Match(content);
                if (tfmMatch.Success)
                {
                    result.CurrentTargetFramework = tfmMatch.Groups[1].Value.Trim();
                    result.TargetFrameworkSource = sourceName;
                }
            }

            // Extract LangVersion
            if (string.IsNullOrEmpty(result.CurrentLangVersion))
            {
                var langMatch = LangVersionPattern.Match(content);
                if (langMatch.Success)
                {
                    result.CurrentLangVersion = langMatch.Groups[1].Value.Trim();
                    result.LangVersionSource = sourceName;
                }
            }
        }
        catch
        {
            // Ignore errors reading files
        }
    }

    private static void ValidateFrameworkCompatibility(TargetFrameworkResult result)
    {
        var version = result.TargetFrameworkVersion;

        if (version == null)
        {
            // Couldn't determine version, assume compatible but needs upgrade
            result.IsTargetFrameworkCompatible = true;
            result.RequiresTargetFrameworkUpgrade = true;
            return;
        }

        // Check if framework is at least the minimum supported
        result.IsTargetFrameworkCompatible = version >= MinimumSupportedFramework;

        // Check if upgrade is required
        result.RequiresTargetFrameworkUpgrade = version < RequiredFramework;
    }

    private static void ValidateLangVersionCompatibility(TargetFrameworkResult result)
    {
        var version = result.LangVersionNumber;

        if (version == null)
        {
            // If no explicit LangVersion, infer from TargetFramework
            var tfmVersion = result.TargetFrameworkVersion;
            if (tfmVersion != null)
            {
                // .NET version roughly corresponds to C# version
                // net8.0 -> C# 12, net9.0 -> C# 13, net10.0 -> C# 14
                version = tfmVersion switch
                {
                    >= 10.0m => 14,
                    >= 9.0m => 13,
                    >= 8.0m => 12,
                    >= 7.0m => 11,
                    _ => 10,
                };
                result.CurrentLangVersion = version.Value.ToString(CultureInfo.InvariantCulture);
                result.LangVersionSource = "inferred from TargetFramework";
            }
            else
            {
                // Assume compatible but needs upgrade
                result.IsLangVersionCompatible = true;
                result.RequiresLangVersionUpgrade = true;
                return;
            }
        }

        // Check if language version is at least the minimum supported
        result.IsLangVersionCompatible = version >= MinimumSupportedLangVersion;

        // Check if upgrade is required
        result.RequiresLangVersionUpgrade = version < RequiredLangVersion;
    }
}
