namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// Reads the direct dependencies of a restored package from its <c>.nuspec</c> in the NuGet global packages folder
/// (<c>NUGET_PACKAGES</c>, else <c>~/.nuget/packages</c>) - what removing the package drops from the build.
/// </summary>
internal static class NuGetPackageDependencies
{
    /// <summary>
    /// Gets the global packages folder.
    /// </summary>
    public static string DefaultPackagesFolder
        => Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

    /// <summary>
    /// Gets the ids of the direct dependencies of <paramref name="packageId"/> <paramref name="version"/> (every target
    /// framework group, distinct, in order), or <see langword="null"/> when the package is not in the folder.
    /// </summary>
    public static IReadOnlyList<string>? Read(
        string packageId,
        string version,
        string packagesFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagesFolder);

        var id = packageId.ToLowerInvariant();
        var nuspec = Path.Combine(packagesFolder, id, version.ToLowerInvariant(), id + ".nuspec");
        if (!File.Exists(nuspec))
        {
            return null;
        }

        try
        {
            return System.Xml.Linq.XDocument.Load(nuspec)
                .Descendants()
                .Where(element => element.Name.LocalName == "dependency")
                .Select(element => (string?)element.Attribute("id"))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }
}