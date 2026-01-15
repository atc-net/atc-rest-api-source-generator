namespace Atc.Rest.Api.Generator.Cli.Services;

/// <summary>
/// Implementation that fetches package versions from the ATC NuGet search API.
/// </summary>
internal sealed class NugetPackageVersionService : INugetPackageVersionService
{
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
            var uri = new Uri($"https://atc-api.azurewebsites.net/nuget-search/package?packageId={Uri.EscapeDataString(packageId)}");
            var response = await httpClient.GetStringAsync(uri, cancellationToken);

            if (Version.TryParse(response.Trim(), out var version))
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
        var version = await GetLatestVersionAsync(packageId, cancellationToken);
        return version ?? fallbackVersion;
    }
}