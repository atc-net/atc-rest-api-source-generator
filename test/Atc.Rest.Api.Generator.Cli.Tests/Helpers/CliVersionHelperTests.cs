namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

public class CliVersionHelperTests
{
    [Fact]
    public async Task GetLatestVersionAsync_LooksUpTheToolPackage()
    {
        var service = new StubNugetPackageVersionService(new Version(2, 3, 4));

        var latest = await CliVersionHelper.GetLatestVersionAsync(service, TestContext.Current.CancellationToken);

        Assert.Equal(new Version(2, 3, 4), latest);
        Assert.Equal("atc-rest-api-gen", service.RequestedPackageId);
    }

    [Fact]
    public Task GetLatestVersionAsync_NullService_Throws()
        => Assert.ThrowsAsync<ArgumentNullException>(() => CliVersionHelper.GetLatestVersionAsync(nugetService: null!, TestContext.Current.CancellationToken));

    [Fact]
    public async Task IsLatestVersionAsync_LookupFails_ReportsLatest()
    {
        // A failed lookup must not nag about an update.
        var service = new StubNugetPackageVersionService(latestVersion: null);

        Assert.True(await CliVersionHelper.IsLatestVersionAsync(service, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsLatestVersionAsync_PublishedVersionIsOlder_ReportsLatest()
    {
        var service = new StubNugetPackageVersionService(new Version(0, 0, 1));

        Assert.True(await CliVersionHelper.IsLatestVersionAsync(service, TestContext.Current.CancellationToken));
    }

    [Fact]
    public Task IsLatestVersionAsync_NullService_Throws()
        => Assert.ThrowsAsync<ArgumentNullException>(() => CliVersionHelper.IsLatestVersionAsync(nugetService: null!, TestContext.Current.CancellationToken));

    private sealed class StubNugetPackageVersionService(Version? latestVersion) : INugetPackageVersionService
    {
        public string? RequestedPackageId { get; private set; }

        public Task<Version?> GetLatestVersionAsync(
            string packageId,
            CancellationToken cancellationToken = default)
        {
            RequestedPackageId = packageId;
            return Task.FromResult(latestVersion);
        }

        public async Task<Version> GetLatestVersionOrFallbackAsync(
            string packageId,
            Version fallbackVersion,
            CancellationToken cancellationToken = default)
            => await GetLatestVersionAsync(packageId, cancellationToken) ?? fallbackVersion;
    }
}