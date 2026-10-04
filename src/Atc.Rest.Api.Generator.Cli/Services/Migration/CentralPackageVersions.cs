namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Central Package Management (<c>Directory.Packages.props</c> with <c>ManagePackageVersionsCentrally</c>): the versions
/// live in the props file and the projects reference packages without a version - a versioned reference there fails
/// the restore of the whole solution (NU1008).
/// </summary>
internal static class CentralPackageVersions
{
    private const string FileName = "Directory.Packages.props";

    /// <summary>
    /// Gets the <c>Directory.Packages.props</c> that manages the versions of <paramref name="projectPath"/>, or
    /// <see langword="null"/> when the project does not use Central Package Management.
    /// </summary>
    public static string? FindFile(string projectPath)
    {
        if (File.Exists(projectPath) &&
            File.ReadAllText(projectPath).Contains("<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        for (var directory = Path.GetDirectoryName(Path.GetFullPath(projectPath)); directory is not null; directory = Path.GetDirectoryName(directory))
        {
            var candidate = Path.Combine(directory, FileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate).Contains("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>", StringComparison.OrdinalIgnoreCase)
                    ? candidate
                    : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Ensures <paramref name="content"/> (a <c>Directory.Packages.props</c>) has a <c>PackageVersion</c> for
    /// <paramref name="packageId"/> of at least <paramref name="minimumVersion"/>: an older one is raised, a missing one
    /// is added.
    /// </summary>
    /// <returns>The updated content.</returns>
    public static string EnsureVersion(
        string content,
        string packageId,
        string minimumVersion,
        ProjectModificationResult result)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(result);

        var pattern = new Regex(
            $@"<PackageVersion\s+Include=""{Regex.Escape(packageId)}""\s+Version=""(?<version>[^""]+)""\s*/>",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));
        var line = $"<PackageVersion Include=\"{packageId}\" Version=\"{minimumVersion}\" />";

        var match = pattern.Match(content);
        if (match.Success)
        {
            var current = match.Groups["version"].Value;
            if (!ProjectFileModifier.IsVersionLessThan(current, minimumVersion))
            {
                return content;
            }

            result.AddedPackages.Add($"{packageId} ({current} → {minimumVersion}, {FileName})");
            return content.Remove(match.Index, match.Length).Insert(match.Index, line);
        }

        result.AddedPackages.Add($"{packageId} ({minimumVersion}, {FileName})");
        var lastVersion = content.LastIndexOf("<PackageVersion ", StringComparison.OrdinalIgnoreCase);
        if (lastVersion >= 0)
        {
            // Next to the other versions, with their indentation
            var lineStart = content.LastIndexOf('\n', lastVersion) + 1;
            var indent = content[lineStart..lastVersion];
            return content.Insert(lineStart, indent + line + Environment.NewLine);
        }

        var projectEnd = content.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
        var group = "  <ItemGroup>" + Environment.NewLine + "    " + line + Environment.NewLine + "  </ItemGroup>" + Environment.NewLine;
        return projectEnd >= 0 ? content.Insert(projectEnd, group) : content + group;
    }
}