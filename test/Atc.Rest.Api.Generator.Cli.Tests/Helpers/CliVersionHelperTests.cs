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

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("1", null, true)]
    [InlineData("true", null, true)]
    [InlineData("0", null, false)]
    [InlineData(null, "true", true)]
    [InlineData(null, "false", false)]
    public void IsUpdateCheckDisabled_FromTheEnvironment(
        string? noUpdateCheck,
        string? ci,
        bool expected)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [CliVersionHelper.NoUpdateCheckVariable] = noUpdateCheck,
            ["CI"] = ci,
        };

        Assert.Equal(expected, CliVersionHelper.IsUpdateCheckDisabled(name => environment.GetValueOrDefault(name)));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("--version")]
    [InlineData("-v")]
    [InlineData("spec validate --help")]
    [InlineData("spec validate -s api.yaml -h")]
    [InlineData("")]
    public void ShouldCheckForUpdates_ReturnsFalse_ForHelpVersionAndNoArguments(
        string commandLine)
    {
        var args = commandLine.Length == 0 ? [] : commandLine.Split(' ');

        Assert.False(CliVersionHelper.ShouldCheckForUpdates(args));
    }

    [Theory]
    [InlineData("spec validate -s api.yaml")]
    [InlineData("generate server -s api.yaml -o out -n Demo")]
    public void ShouldCheckForUpdates_ReturnsTrue_ForCommands(
        string commandLine)
        => Assert.True(CliVersionHelper.ShouldCheckForUpdates(commandLine.Split(' ')));

    [Fact]
    public async Task TryGetNewerVersionAsync_NewerVersionOnTheFeed_ReturnsIt()
    {
        var service = new StubNugetPackageVersionService(new Version(9, 9, 9));

        var version = await CliVersionHelper.TryGetNewerVersionAsync(service, new Version(1, 2, 3), Budget);

        Assert.Equal(new Version(9, 9, 9), version);
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("0.0.1")]
    [InlineData(null)]
    public async Task TryGetNewerVersionAsync_CurrentIsLatestOrLookupFails_ReturnsNull(
        string? published)
    {
        var service = new StubNugetPackageVersionService(published is null ? null : Version.Parse(published));

        var version = await CliVersionHelper.TryGetNewerVersionAsync(service, new Version(1, 2, 3), Budget);

        Assert.Null(version);
    }

    [Fact]
    public async Task TryGetNewerVersionAsync_LookupThrows_ReturnsNull()
    {
        var service = new StubNugetPackageVersionService(new Version(9, 9, 9), throwOnLookup: true);

        var version = await CliVersionHelper.TryGetNewerVersionAsync(service, new Version(1, 2, 3), Budget);

        Assert.Null(version);
    }

    [Fact]
    public async Task TryGetNewerVersionAsync_LookupNeverCompletes_ReturnsNullWithinBudget()
    {
        var lookup = TimeSpan.FromSeconds(30);
        var service = new StubNugetPackageVersionService(new Version(9, 9, 9), delay: lookup);
        var stopwatch = Stopwatch.StartNew();

        var version = await CliVersionHelper.TryGetNewerVersionAsync(service, new Version(1, 2, 3), Budget);

        stopwatch.Stop();
        Assert.Null(version);

        // The budget ended the wait, not the lookup: well short of it even when a loaded machine runs the timer late
        // (a full parallel test run once took 5.03s for the 2s budget)
        Assert.True(stopwatch.Elapsed < lookup / 2, $"Took {stopwatch.Elapsed}.");
    }

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    private sealed class StubNugetPackageVersionService(
        Version? latestVersion,
        bool throwOnLookup = false,
        TimeSpan? delay = null) : INugetPackageVersionService
    {
        public string? RequestedPackageId { get; private set; }

        public async Task<Version?> GetLatestVersionAsync(
            string packageId,
            CancellationToken cancellationToken = default)
        {
            RequestedPackageId = packageId;
            if (throwOnLookup)
            {
                throw new InvalidOperationException("The feed answered nonsense.");
            }

            if (delay is { } wait)
            {
                // Ignores the token on purpose: the budget must hold even when the lookup does not cooperate.
                await Task.Delay(wait, CancellationToken.None);
            }

            return latestVersion;
        }

        public async Task<Version> GetLatestVersionOrFallbackAsync(
            string packageId,
            Version fallbackVersion,
            CancellationToken cancellationToken = default)
            => await GetLatestVersionAsync(packageId, cancellationToken) ?? fallbackVersion;
    }
}