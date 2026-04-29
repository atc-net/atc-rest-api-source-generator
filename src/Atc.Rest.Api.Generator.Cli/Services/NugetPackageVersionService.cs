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

    public NugetPackageVersionService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

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

    public async Task<Version> GetLatestVersionOrFallbackAsync(
        string packageId,
        Version fallbackVersion,
        CancellationToken cancellationToken = default)
    {
        var version = await GetLatestVersionAsync(packageId, cancellationToken).ConfigureAwait(false);
        return version ?? fallbackVersion;
    }
}