namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// Implementation that fetches package versions from the NuGet.org flat container API.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A version lookup is best effort: any failure (network, captive portal, malformed feed) means 'unknown', never a crash.")]
public sealed class NugetPackageVersionService : INugetPackageVersionService
{
    [SuppressMessage("", "S1075:Refactor your code not to use hardcoded absolute paths or URIs.", Justification = "OK")]
    private const string NugetFlatContainerBaseUrl = "https://api.nuget.org/v3-flatcontainer";
    private readonly ConcurrentDictionary<string, Version> versionCache = new(StringComparer.Ordinal);
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
    /// Gets the latest stable version of a NuGet package from the flat container index, caching it per instance.
    /// Any failure - no network, a non-success status, a captive portal's HTML, a feed whose shape is not the
    /// expected one, a timeout or a cancellation - means the version is unknown; nothing is thrown.
    /// </summary>
    /// <param name="packageId">The package ID (e.g., "Atc.Rest.Client").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest stable version, or null when the lookup fails or times out.</returns>
    public async Task<Version?> GetLatestVersionAsync(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        if (versionCache.TryGetValue(packageId, out var cached))
        {
            return cached;
        }

        try
        {
            var uri = new Uri($"{NugetFlatContainerBaseUrl}/{packageId.ToLowerInvariant()}/index.json");
            using var response = await httpClient
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            var version = ParseLatestStableVersion(json);
            if (version is not null)
            {
                versionCache.TryAdd(packageId, version);
            }

            return version;
        }
        catch (Exception)
        {
            return null;
        }
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

    private static Version? ParseLatestStableVersion(string json)
    {
        var versions = JsonNode.Parse(json)?["versions"]?.AsArray();
        if (versions is null || versions.Count == 0)
        {
            return null;
        }

        // The newest stable version: a prerelease ("3.0.0-preview.1") is never offered to a scaffold,
        // and an entry that is not a version string is skipped.
        return versions
            .Select(node => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null)
            .Where(text => text is not null && !text.Contains('-', StringComparison.Ordinal))
            .Select(text => Version.TryParse(text, out var parsed) ? parsed : null)
            .Where(parsed => parsed is not null)
            .Max();
    }
}