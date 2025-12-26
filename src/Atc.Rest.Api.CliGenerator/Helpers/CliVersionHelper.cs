namespace Atc.Rest.Api.CliGenerator.Helpers;

/// <summary>
/// Helper for checking CLI version information.
/// </summary>
public static class CliVersionHelper
{
    private const string PackageId = "atc-rest-api-gen";

    /// <summary>
    /// Gets the latest available version from NuGet.
    /// </summary>
    /// <returns>The latest version, or null if unavailable.</returns>
    public static Version? GetLatestVersion()
        => AtcApiNugetClientHelper.GetLatestVersionForPackageId(PackageId);

    /// <summary>
    /// Checks if the current version is the latest available.
    /// </summary>
    /// <returns>True if current version is latest or if check fails.</returns>
    public static bool IsLatestVersion()
    {
        var currentVersion = CliHelper.GetCurrentVersion();

        // Skip check for development versions
        if (currentVersion == new Version(1, 0, 0, 0))
        {
            return true;
        }

        var latestVersion = GetLatestVersion();
        return latestVersion is null ||
               !latestVersion.GreaterThan(currentVersion);
    }
}