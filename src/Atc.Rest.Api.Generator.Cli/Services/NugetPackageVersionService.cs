namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// Implementation that fetches package versions from the NuGet.org flat container API.
/// </summary>
public sealed class NugetPackageVersionService : INugetPackageVersionService
{
    [SuppressMessage("", "S1075:Refactor your code not to use hardcoded absolute paths or URIs.", Justification = "OK")]
    private const string NugetFlatContainerBaseUrl = "https://api.nuget.org/v3-flatcontainer";
    private static readonly ConcurrentDictionary<string, Version> VersionCache = new(StringComparer.Ordinal);
    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="NugetPackageVersionService"/> class.
    /// </summary>
    /// <param name="httpClient">The client the NuGet flat container index is fetched with.</param>
    public NugetPackageVersionService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    /// <summary>
    /// Gets the latest version of a NuGet package from the flat container index, caching it per process.
    /// </summary>
    /// <param name="packageId">The package ID (e.g., "Atc.Rest.Client").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest version, or null when the lookup fails or times out.</returns>
    public async Task<Version?> GetLatestVersionAsync(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        if (VersionCache.TryGetValue(packageId, out var cached))
        {
            return cached;
        }

        try
        {
            var uri = new Uri($"{NugetFlatContainerBaseUrl}/{packageId.ToLowerInvariant()}/index.json");
            var response = await httpClient
                .GetStringAsync(uri, cancellationToken)
                .ConfigureAwait(false);

            var jsonNode = JsonNode.Parse(response);
            var versions = jsonNode?["versions"]?.AsArray();
            if (versions is null || versions.Count == 0)
            {
                return null;
            }

            var latestVersionString = versions[^1]?.GetValue<string>();
            if (latestVersionString is not null &&
                Version.TryParse(latestVersionString, out var version))
            {
                VersionCache.TryAdd(packageId, version);
                return version;
            }
        }
        catch (HttpRequestException)
        {
            // Network error - return null
        }
        catch (TaskCanceledException)
        {
            // Timeout or cancellation - return null
        }

        return null;
    }

    /// <summary>
    /// Gets the latest version of a NuGet package, with a fallback if lookup fails.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="fallbackVersion">The fallback version to use if lookup fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest version or the fallback version.</returns>
    public async Task<Version> GetLatestVersionOrFallbackAsync(
        string packageId,
        Version fallbackVersion,
        CancellationToken cancellationToken = default)
    {
        var version = await GetLatestVersionAsync(packageId, cancellationToken).ConfigureAwait(false);
        return version ?? fallbackVersion;
    }
}