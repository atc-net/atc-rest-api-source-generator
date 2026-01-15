namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// Service for looking up the latest NuGet package versions from the ATC API.
/// </summary>
internal interface INugetPackageVersionService
{
    /// <summary>
    /// Gets the latest version of a NuGet package.
    /// </summary>
    /// <param name="packageId">The package ID (e.g., "Atc.Rest.Client").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest version, or null if lookup fails.</returns>
    Task<Version?> GetLatestVersionAsync(
        string packageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest version of a NuGet package, with a fallback if lookup fails.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="fallbackVersion">The fallback version to use if lookup fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest version or the fallback version.</returns>
    Task<Version> GetLatestVersionOrFallbackAsync(
        string packageId,
        Version fallbackVersion,
        CancellationToken cancellationToken = default);
}