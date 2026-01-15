namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Analyzes package references in project files.
/// </summary>
internal static class PackageReferenceAnalyzer
{
    private static readonly Regex PackageReferencePattern = new(
        @"<PackageReference\s+Include=""(?<package>[^""]+)""\s+Version=""(?<version>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    private static readonly string[] AtcPackagePrefixes =
    [
        "Atc",
        "Atc.Rest",
        "Atc.Rest.MinimalApi",
        "Atc.Rest.Client",
        "Atc.Rest.Extended",
        "Atc.OpenApi",
    ];

    /// <summary>
    /// Analyzes package references in the given project files.
    /// </summary>
    /// <param name="projectFiles">List of .csproj file paths to analyze.</param>
    /// <returns>The package reference analysis result.</returns>
    public static PackageReferenceResult Analyze(
        IEnumerable<string> projectFiles)
    {
        var result = new PackageReferenceResult();

        foreach (var projectFile in projectFiles)
        {
            AnalyzeProjectFile(projectFile, result);
        }

        // Extract specific Atc package versions
        if (result.AtcPackages.TryGetValue("Atc", out var atcVersion))
        {
            result.AtcVersion = atcVersion;
        }

        if (result.AtcPackages.TryGetValue("Atc.Rest", out var atcRestVersion))
        {
            result.AtcRestVersion = atcRestVersion;
        }

        if (result.AtcPackages.TryGetValue("Atc.Rest.MinimalApi", out var atcMinimalApiVersion))
        {
            result.AtcRestMinimalApiVersion = atcMinimalApiVersion;
        }

        if (result.AtcPackages.TryGetValue("Atc.Rest.Client", out var atcClientVersion))
        {
            result.AtcRestClientVersion = atcClientVersion;
        }

        // Identify packages that need to be removed during migration
        IdentifyPackagesToRemove(result);

        return result;
    }

    private static void AnalyzeProjectFile(
        string projectFile,
        PackageReferenceResult result)
    {
        if (!File.Exists(projectFile))
        {
            return;
        }

        try
        {
            var content = File.ReadAllText(projectFile);
            var matches = PackageReferencePattern.Matches(content);

            foreach (Match match in matches)
            {
                var packageName = match.Groups["package"].Value;
                var packageVersion = match.Groups["version"].Value;

                if (IsAtcPackage(packageName))
                {
                    result.AtcPackages.TryAdd(packageName, packageVersion);
                }
                else
                {
                    result.OtherPackages.TryAdd(packageName, packageVersion);
                }
            }
        }
        catch
        {
            // Ignore errors reading project files
        }
    }

    private static bool IsAtcPackage(string packageName)
        => AtcPackagePrefixes.Any(prefix =>
            packageName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static void IdentifyPackagesToRemove(PackageReferenceResult result)
    {
        // These packages will be replaced by the source generator
        // Note: Atc.Rest.Client is NOT removed - it's required by generated client code
        string[] packagesToRemove =
        [
            "Atc.Rest.MinimalApi",
            "Atc.Rest.Extended.Options",
        ];

        foreach (var package in packagesToRemove)
        {
            if (result.AtcPackages.ContainsKey(package))
            {
                result.PackagesToRemove.Add(package);
            }
        }
    }
}