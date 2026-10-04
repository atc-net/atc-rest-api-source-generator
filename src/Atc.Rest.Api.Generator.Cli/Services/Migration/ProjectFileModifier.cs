namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Modifies project files (.csproj) for source generator migration.
/// </summary>
internal static class ProjectFileModifier
{
    /// <summary>
    /// Modifies the Api.Generated (soon to be Api.Contracts) project file.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="specificationRelativePath">Relative path to the OpenAPI specification.</param>
    /// <param name="sourceGeneratorVersion">Version of the source generator package to use.</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The modifications made.</returns>
    public static ProjectModificationResult ModifyServerProject(
        string projectPath,
        string specificationRelativePath,
        Version sourceGeneratorVersion,
        bool dryRun = false)
    {
        var result = new ProjectModificationResult { ProjectPath = projectPath };

        if (!File.Exists(projectPath))
        {
            result.Error = "Project file not found.";
            return result;
        }

        var content = File.ReadAllText(projectPath);
        var modified = content;
        var versionString = sourceGeneratorVersion.ToString();
        var centralFile = CentralPackageVersions.FindFile(projectPath);
        var centralContent = centralFile is null ? null : File.ReadAllText(centralFile);
        var central = centralContent;

        // Add source generator package reference
        if (!content.Contains(PackageVersionDefaults.SourceGeneratorPackageId, StringComparison.OrdinalIgnoreCase))
        {
            var packageRef = GenerateSourceGeneratorPackageReference(central is null ? versionString : null);
            modified = AddItemGroup(modified, packageRef);
            if (central is null)
            {
                result.AddedPackages.Add($"{PackageVersionDefaults.SourceGeneratorPackageId} ({versionString})");
            }
        }

        // With Central Package Management the version goes to Directory.Packages.props
        if (central is not null)
        {
            central = CentralPackageVersions.EnsureVersion(central, PackageVersionDefaults.SourceGeneratorPackageId, versionString, result);
        }

        // Add AdditionalFiles for spec and marker
        if (!content.Contains("<AdditionalFiles", StringComparison.OrdinalIgnoreCase))
        {
            var additionalFiles = GenerateAdditionalFilesItemGroup(specificationRelativePath, ".atc-rest-api-server");
            modified = AddItemGroup(modified, Environment.NewLine + additionalFiles + Environment.NewLine);
            result.AddedAdditionalFiles.Add(specificationRelativePath);
            result.AddedAdditionalFiles.Add(".atc-rest-api-server");
        }

        if (!dryRun && modified != content)
        {
            File.WriteAllText(projectPath, modified);
        }

        if (!dryRun && centralFile is not null && central != centralContent)
        {
            File.WriteAllText(centralFile, central);
        }

        result.WasModified = modified != content || central != centralContent;
        return result;
    }

    /// <summary>
    /// Modifies the ApiClient.Generated (soon to be ApiClient) project file.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="specificationRelativePath">Relative path to the OpenAPI specification.</param>
    /// <param name="sourceGeneratorVersion">Version of the source generator package to use.</param>
    /// <param name="restClientMinVersion">Minimum version of Atc.Rest.Client required.</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The modifications made.</returns>
    public static ProjectModificationResult ModifyClientProject(
        string projectPath,
        string specificationRelativePath,
        Version sourceGeneratorVersion,
        Version restClientMinVersion,
        bool dryRun = false)
    {
        var result = new ProjectModificationResult { ProjectPath = projectPath };

        if (!File.Exists(projectPath))
        {
            result.Error = "Project file not found.";
            return result;
        }

        var content = File.ReadAllText(projectPath);
        var modified = content;
        var versionString = sourceGeneratorVersion.ToString();
        var centralFile = CentralPackageVersions.FindFile(projectPath);
        var centralContent = centralFile is null ? null : File.ReadAllText(centralFile);
        var central = centralContent;
        var clientVersionString = restClientMinVersion.ToString();

        // Add source generator package reference
        if (!content.Contains(PackageVersionDefaults.SourceGeneratorPackageId, StringComparison.OrdinalIgnoreCase))
        {
            var packageRef = GenerateSourceGeneratorPackageReference(central is null ? versionString : null);
            modified = AddItemGroup(modified, packageRef);
            if (central is null)
            {
                result.AddedPackages.Add($"{PackageVersionDefaults.SourceGeneratorPackageId} ({versionString})");
            }
        }

        // With Central Package Management the version goes to Directory.Packages.props
        if (central is not null)
        {
            central = CentralPackageVersions.EnsureVersion(central, PackageVersionDefaults.SourceGeneratorPackageId, versionString, result);
        }

        // Add AdditionalFiles for spec and marker
        if (!content.Contains("<AdditionalFiles", StringComparison.OrdinalIgnoreCase))
        {
            var additionalFiles = GenerateAdditionalFilesItemGroup(specificationRelativePath, ".atc-rest-api-client");
            modified = AddItemGroup(modified, Environment.NewLine + additionalFiles + Environment.NewLine);
            result.AddedAdditionalFiles.Add(specificationRelativePath);
            result.AddedAdditionalFiles.Add(".atc-rest-api-client");
        }

        // Ensure Atc.Rest.Client >= minimum version (required for generated client code)
        modified = EnsureMinimumPackageVersion(
            modified,
            PackageVersionDefaults.RestClientPackageId,
            clientVersionString,
            result,
            ref central);

        if (!dryRun && modified != content)
        {
            File.WriteAllText(projectPath, modified);
        }

        if (!dryRun && centralFile is not null && central != centralContent)
        {
            File.WriteAllText(centralFile, central);
        }

        result.WasModified = modified != content || central != centralContent;
        return result;
    }

    /// <summary>
    /// Modifies the Domain project file.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="specificationRelativePath">Relative path to the OpenAPI specification.</param>
    /// <param name="sourceGeneratorVersion">Version of the source generator package to use.</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The modifications made.</returns>
    public static ProjectModificationResult ModifyDomainProject(
        string projectPath,
        string specificationRelativePath,
        Version sourceGeneratorVersion,
        bool dryRun = false)
    {
        var result = new ProjectModificationResult { ProjectPath = projectPath };

        if (!File.Exists(projectPath))
        {
            result.Error = "Project file not found.";
            return result;
        }

        var content = File.ReadAllText(projectPath);
        var modified = content;
        var versionString = sourceGeneratorVersion.ToString();
        var centralFile = CentralPackageVersions.FindFile(projectPath);
        var centralContent = centralFile is null ? null : File.ReadAllText(centralFile);
        var central = centralContent;

        // Add source generator package reference
        if (!content.Contains(PackageVersionDefaults.SourceGeneratorPackageId, StringComparison.OrdinalIgnoreCase))
        {
            var packageRef = GenerateSourceGeneratorPackageReference(central is null ? versionString : null);
            modified = AddItemGroup(modified, packageRef);
            if (central is null)
            {
                result.AddedPackages.Add($"{PackageVersionDefaults.SourceGeneratorPackageId} ({versionString})");
            }
        }

        // With Central Package Management the version goes to Directory.Packages.props
        if (central is not null)
        {
            central = CentralPackageVersions.EnsureVersion(central, PackageVersionDefaults.SourceGeneratorPackageId, versionString, result);
        }

        // Add AdditionalFiles for spec and marker
        if (!content.Contains(".atc-rest-api-server-handlers", StringComparison.OrdinalIgnoreCase))
        {
            var additionalFiles = GenerateAdditionalFilesItemGroup(specificationRelativePath, ".atc-rest-api-server-handlers");
            modified = AddItemGroup(modified, Environment.NewLine + additionalFiles + Environment.NewLine);
            result.AddedAdditionalFiles.Add(specificationRelativePath);
            result.AddedAdditionalFiles.Add(".atc-rest-api-server-handlers");
        }

        if (!dryRun && modified != content)
        {
            File.WriteAllText(projectPath, modified);
        }

        if (!dryRun && centralFile is not null && central != centralContent)
        {
            File.WriteAllText(centralFile, central);
        }

        result.WasModified = modified != content || central != centralContent;
        return result;
    }

    /// <summary>
    /// Compares two package versions (x.y.z); falls back to an ordinal comparison when one is no version.
    /// </summary>
    internal static bool IsVersionLessThan(
        string current,
        string minimum)
    {
        if (Version.TryParse(current, out var currentVer) &&
            Version.TryParse(minimum, out var minimumVer))
        {
            return currentVer < minimumVer;
        }

        return string.Compare(current, minimum, StringComparison.OrdinalIgnoreCase) < 0;
    }

    // A null version: the solution manages versions centrally, the reference carries none
    private static string GenerateSourceGeneratorPackageReference(
        string? version)
        => version is null
            ? $"""
                  <ItemGroup>
                    <PackageReference Include="{PackageVersionDefaults.SourceGeneratorPackageId}"
                                      OutputItemType="Analyzer"
                                      ReferenceOutputAssembly="false" />
                  </ItemGroup>
                """
            : $"""
                  <ItemGroup>
                    <PackageReference Include="{PackageVersionDefaults.SourceGeneratorPackageId}"
                                      Version="{version}"
                                      OutputItemType="Analyzer"
                                      ReferenceOutputAssembly="false" />
                  </ItemGroup>
                """;

    private static string GenerateAdditionalFilesItemGroup(
        string specPath,
        string markerFile)
        => $"""
              <ItemGroup>
                <AdditionalFiles Include="{specPath}" />
                <AdditionalFiles Include="{markerFile}" />
              </ItemGroup>
            """;

    private static string AddItemGroup(
        string content,
        string itemGroup)
    {
        // Find the closing </Project> tag and insert before it
        var projectEndIndex = content.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
        if (projectEndIndex >= 0)
        {
            return content.Insert(projectEndIndex, itemGroup + Environment.NewLine);
        }

        return content + itemGroup;
    }

    private static string EnsureMinimumPackageVersion(
        string content,
        string packageName,
        string minVersion,
        ProjectModificationResult result,
        ref string? central)
    {
        // A reference with or without a version (Central Package Management writes none)
        var reference = new Regex(
            $@"<PackageReference\s+Include=""{Regex.Escape(packageName)}""(?<rest>[^>]*?)/?>",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));
        var version = new Regex(
            @"\bVersion=""(?<version>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));

        if (central is not null)
        {
            central = CentralPackageVersions.EnsureVersion(central, packageName, minVersion, result);
            return reference.IsMatch(content)
                ? content
                : AddItemGroup(content, $"""
                      <ItemGroup>
                        <PackageReference Include="{packageName}" />
                      </ItemGroup>
                    """);
        }

        var match = reference.Match(content);
        if (!match.Success)
        {
            result.AddedPackages.Add($"{packageName} ({minVersion})");
            return AddItemGroup(content, $"""
                  <ItemGroup>
                    <PackageReference Include="{packageName}" Version="{minVersion}" />
                  </ItemGroup>
                """);
        }

        var versionMatch = version.Match(match.Groups["rest"].Value);
        if (versionMatch.Success && IsVersionLessThan(versionMatch.Groups["version"].Value, minVersion))
        {
            var current = versionMatch.Groups["version"].Value;
            result.AddedPackages.Add($"{packageName} ({current} → {minVersion})");
            var updatedReference = match.Value.Replace($"Version=\"{current}\"", $"Version=\"{minVersion}\"", StringComparison.Ordinal);
            return content.Remove(match.Index, match.Length).Insert(match.Index, updatedReference);
        }

        return content;
    }
}