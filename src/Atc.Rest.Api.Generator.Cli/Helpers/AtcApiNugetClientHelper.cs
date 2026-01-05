namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Helper for fetching package version information from the ATC NuGet API.
/// </summary>
public static class AtcApiNugetClientHelper
{
    private const string BaseAddress = "https://atc-api.azurewebsites.net/nuget-search";
    private static readonly ConcurrentDictionary<string, Version> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the latest version for the specified NuGet package.
    /// </summary>
    /// <param name="packageId">The NuGet package ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest version, or null if not found or on error.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
    public static Version? GetLatestVersionForPackageId(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        var cacheValue = Cache.GetValueOrDefault(packageId);
        if (cacheValue is not null)
        {
            return cacheValue;
        }

        try
        {
            var response = string.Empty;
            var uri = new Uri($"{BaseAddress}/package?packageId={packageId}");
            TaskHelper.RunSync(async () =>
            {
                using var client = new HttpClient();
                response = await client.GetStringAsync(uri, cancellationToken);
            });

            if (string.IsNullOrEmpty(response) ||
                !Version.TryParse(response, out var version))
            {
                return null;
            }

            Cache.GetOrAdd(packageId, version);
            return version;
        }
        catch
        {
            return null;
        }
    }
}