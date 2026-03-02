namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Helper for fetching package version information from the NuGet.org flat container API.
/// </summary>
public static class NugetApiClientHelper
{
    private const string BaseAddress = "https://api.nuget.org/v3-flatcontainer";
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
            var uri = new Uri($"{BaseAddress}/{packageId.ToLowerInvariant()}/index.json");
            TaskHelper.RunSync(async () =>
            {
                using var client = new HttpClient();
                response = await client.GetStringAsync(uri, cancellationToken);
            });

            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            var jsonNode = JsonNode.Parse(response);
            var versions = jsonNode?["versions"]?.AsArray();
            if (versions is null || versions.Count == 0)
            {
                return null;
            }

            var latestVersionString = versions[^1]?.GetValue<string>();
            if (latestVersionString is null ||
                !Version.TryParse(latestVersionString, out var version))
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