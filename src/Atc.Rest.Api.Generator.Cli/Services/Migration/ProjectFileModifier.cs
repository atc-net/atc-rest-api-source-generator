namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Modifies project files (.csproj) for source generator migration.
/// </summary>
internal static class ProjectFileModifier
{
    private const string SourceGeneratorPackage = "Atc.Rest.Api.SourceGenerator";
    private const string SourceGeneratorVersion = "1.0.59";

    private const string AtcRestClientPackage = "Atc.Rest.Client";
    private const string AtcRestClientMinVersion = "2.0.12";

    /// <summary>
    /// Modifies the Api.Generated (soon to be Api.Contracts) project file.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="specificationRelativePath">Relative path to the OpenAPI specification.</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The modifications made.</returns>
    public static ProjectModificationResult ModifyServerProject(
        string projectPath,
        string specificationRelativePath,
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

        // Add source generator package reference
        if (!content.Contains(SourceGeneratorPackage, StringComparison.OrdinalIgnoreCase))
        {
            var packageRef = GenerateSourceGeneratorPackageReference();
            modified = AddItemGroup(modified, packageRef);
            result.AddedPackages.Add($"{SourceGeneratorPackage} ({SourceGeneratorVersion})");
        }

        // Add AdditionalFiles for spec and marker
        if (!content.Contains("<AdditionalFiles", StringComparison.OrdinalIgnoreCase))
        {
            var additionalFiles = GenerateAdditionalFilesItemGroup(specificationRelativePath, ".atc-rest-api-server");
            modified = AddItemGroup(modified, additionalFiles);
            result.AddedAdditionalFiles.Add(specificationRelativePath);
            result.AddedAdditionalFiles.Add(".atc-rest-api-server");
        }

        if (!dryRun && modified != content)
        {
            File.WriteAllText(projectPath, modified);
        }

        result.WasModified = modified != content;
        return result;
    }

    /// <summary>
    /// Modifies the ApiClient.Generated (soon to be ApiClient) project file.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="specificationRelativePath">Relative path to the OpenAPI specification.</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The modifications made.</returns>
    public static ProjectModificationResult ModifyClientProject(
        string projectPath,
        string specificationRelativePath,
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

        // Add source generator package reference
        if (!content.Contains(SourceGeneratorPackage, StringComparison.OrdinalIgnoreCase))
        {
            var packageRef = GenerateSourceGeneratorPackageReference();
            modified = AddItemGroup(modified, packageRef);
            result.AddedPackages.Add($"{SourceGeneratorPackage} ({SourceGeneratorVersion})");
        }

        // Add AdditionalFiles for spec and marker
        if (!content.Contains("<AdditionalFiles", StringComparison.OrdinalIgnoreCase))
        {
            var additionalFiles = GenerateAdditionalFilesItemGroup(specificationRelativePath, ".atc-rest-api-client");
            modified = AddItemGroup(modified, additionalFiles);
            result.AddedAdditionalFiles.Add(specificationRelativePath);
            result.AddedAdditionalFiles.Add(".atc-rest-api-client");
        }

        // Ensure Atc.Rest.Client >= minimum version (required for generated client code)
        modified = EnsureMinimumPackageVersion(modified, AtcRestClientPackage, AtcRestClientMinVersion, result);

        if (!dryRun && modified != content)
        {
            File.WriteAllText(projectPath, modified);
        }

        result.WasModified = modified != content;
        return result;
    }

    /// <summary>
    /// Modifies the Domain project file.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="specificationRelativePath">Relative path to the OpenAPI specification.</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The modifications made.</returns>
    public static ProjectModificationResult ModifyDomainProject(
        string projectPath,
        string specificationRelativePath,
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

        // Add source generator package reference
        if (!content.Contains(SourceGeneratorPackage, StringComparison.OrdinalIgnoreCase))
        {
            var packageRef = GenerateSourceGeneratorPackageReference();
            modified = AddItemGroup(modified, packageRef);
            result.AddedPackages.Add($"{SourceGeneratorPackage} ({SourceGeneratorVersion})");
        }

        // Add AdditionalFiles for spec and marker
        if (!content.Contains(".atc-rest-api-server-handlers", StringComparison.OrdinalIgnoreCase))
        {
            var additionalFiles = GenerateAdditionalFilesItemGroup(specificationRelativePath, ".atc-rest-api-server-handlers");
            modified = AddItemGroup(modified, additionalFiles);
            result.AddedAdditionalFiles.Add(specificationRelativePath);
            result.AddedAdditionalFiles.Add(".atc-rest-api-server-handlers");
        }

        if (!dryRun && modified != content)
        {
            File.WriteAllText(projectPath, modified);
        }

        result.WasModified = modified != content;
        return result;
    }

    /// <summary>
    /// Updates project references from old names to new names.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="oldName">The old project name (e.g., "Api.Generated").</param>
    /// <param name="newName">The new project name (e.g., "Api.Contracts").</param>
    /// <param name="dryRun">If true, only returns what would be modified.</param>
    /// <returns>The modifications made.</returns>
    public static ProjectModificationResult UpdateProjectReference(
        string projectPath,
        string oldName,
        string newName,
        bool dryRun = false)
    {
        var result = new ProjectModificationResult { ProjectPath = projectPath };

        if (!File.Exists(projectPath))
        {
            result.Error = "Project file not found.";
            return result;
        }

        var content = File.ReadAllText(projectPath);

        if (!content.Contains(oldName, StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        var modified = content.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
        result.UpdatedReferences.Add($"{oldName} → {newName}");

        if (!dryRun && modified != content)
        {
            File.WriteAllText(projectPath, modified);
        }

        result.WasModified = modified != content;
        return result;
    }

    private static string GenerateSourceGeneratorPackageReference()
        => $"""
              <ItemGroup>
                <PackageReference Include="{SourceGeneratorPackage}"
                                  Version="{SourceGeneratorVersion}"
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
        ProjectModificationResult result)
    {
        // Pattern to find the package reference with version
        var pattern = new Regex(
            $@"<PackageReference\s+Include=""{Regex.Escape(packageName)}""\s+Version=""(?<version>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));

        var match = pattern.Match(content);

        if (match.Success)
        {
            // Package exists - check if version needs update
            var currentVersion = match.Groups["version"].Value;
            if (IsVersionLessThan(currentVersion, minVersion))
            {
                // Update the version
                var updated = content.Remove(match.Index, match.Length);
                updated = updated.Insert(match.Index, $"<PackageReference Include=\"{packageName}\" Version=\"{minVersion}\"");
                result.AddedPackages.Add($"{packageName} ({currentVersion} → {minVersion})");
                return updated;
            }
        }
        else
        {
            // Package doesn't exist - add it
            var packageRef = $"""
                  <ItemGroup>
                    <PackageReference Include="{packageName}" Version="{minVersion}" />
                  </ItemGroup>
                """;
            result.AddedPackages.Add($"{packageName} ({minVersion})");
            return AddItemGroup(content, packageRef);
        }

        return content;
    }

    private static bool IsVersionLessThan(
        string current,
        string minimum)
    {
        // Simple version comparison - handles x.y.z format
        if (Version.TryParse(current, out var currentVer) &&
            Version.TryParse(minimum, out var minimumVer))
        {
            return currentVer < minimumVer;
        }

        // Fall back to string comparison if parsing fails
        return string.Compare(current, minimum, StringComparison.OrdinalIgnoreCase) < 0;
    }
}