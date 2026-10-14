namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Analyzes package references in project files.
/// </summary>
internal static class PackageReferenceAnalyzer
{
    private static readonly Regex PackageReferencePattern = new(
        @"<PackageReference\s+Include=""(?<package>[^""]+)""(?:\s+Version=""(?<version>[^""]+)"")?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    // The packages the generated code builds on: the version it needs, and the build error an older one gives
    private static readonly (string Package, Version Minimum, string RuleId)[] MinimumVersions =
    [
        ("Atc.Rest.MinimalApi", PackageVersionDefaults.MinimalApiMin, "ATC_API_DEP013"),
        ("Atc.Rest.Client", PackageVersionDefaults.RestClientMinFallback, "ATC_API_DEP011"),
    ];

    private static readonly char[] VersionSuffixStart = ['-', '+'];

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
        IdentifyPackagesToUpgrade(result);

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

            // A versionless reference takes its version from Directory.Packages.props (Central Package Management)
            var centralFile = CentralPackageVersions.FindFile(projectFile);
            var centralContent = centralFile is null ? null : File.ReadAllText(centralFile);

            foreach (Match match in matches)
            {
                var packageName = match.Groups["package"].Value;
                var packageVersion = match.Groups["version"].Success
                    ? match.Groups["version"].Value
                    : CentralVersion(centralContent, packageName) ?? string.Empty;

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
        // The old generator's runtime that the source generator replaces. Atc.Rest.MinimalApi (the server code builds
        // on it) and Atc.Rest.Client (the client code) are upgraded, not obsolete.
        string[] packagesToRemove =
        [
            "Atc.Rest.Extended",
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

    // The packages the generated code builds on, in the version it needs: an older one stops the first build with the
    // generator's error, so the migration names it beforehand
    private static void IdentifyPackagesToUpgrade(PackageReferenceResult result)
    {
        foreach (var (package, minimum, ruleId) in MinimumVersions)
        {
            if (result.AtcPackages.TryGetValue(package, out var referenced) &&
                ReleaseVersionOf(referenced) is { } version &&
                version < minimum)
            {
                result.PackagesToUpgrade.Add(new PackageUpgrade(package, referenced, minimum.ToString(3), ruleId));
            }
        }
    }

    // A version as NuGet writes it, without its pre-release and build parts ("2.7.1-preview.3" is 2.7.1); null for a
    // property or a range the project resolves at build time
    private static Version? ReleaseVersionOf(string version)
    {
        var release = version.Split(VersionSuffixStart)[0];
        return Version.TryParse(release, out var parsed)
            ? new Version(parsed.Major, parsed.Minor, System.Math.Max(parsed.Build, 0))
            : null;
    }

    private static string? CentralVersion(
        string? centralContent,
        string packageName)
    {
        if (centralContent is null)
        {
            return null;
        }

        var match = Regex.Match(
            centralContent,
            $@"<PackageVersion\s+Include=""{Regex.Escape(packageName)}""\s+Version=""(?<version>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));
        return match.Success ? match.Groups["version"].Value : null;
    }
}