namespace Atc.Rest.Api.Generator.Cli.Tests.Services;

/// <summary>
/// The version lookup runs before every command, so a bad answer from the network must never
/// throw or stall (HARD-51), and a prerelease is never offered to a scaffold.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The HttpClient owns the stub handler; both live for the test only.")]
public sealed class NugetPackageVersionServiceTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task GetLatestVersionAsync_NewestIsAPrerelease_ReturnsTheNewestStableVersion()
    {
        var service = CreateService("""{ "versions": [ "1.0.341", "2.1.0", "3.0.0-preview.1" ] }""");

        var version = await service.GetLatestVersionAsync("Contoso.Package.Prerelease", TestContext.Current.CancellationToken);

        Assert.Equal(new Version(2, 1, 0), version);
    }

    [Fact]
    public async Task GetLatestVersionAsync_OnlyPrereleases_ReturnsNull()
    {
        var service = CreateService("""{ "versions": [ "3.0.0-preview.1", "3.0.0-rc.1" ] }""");

        var version = await service.GetLatestVersionAsync("Contoso.Package.OnlyPrerelease", TestContext.Current.CancellationToken);

        Assert.Null(version);
    }

    [Fact]
    public async Task GetLatestVersionOrFallbackAsync_LookupFails_ReturnsTheFallback()
    {
        var service = new NugetPackageVersionService(new HttpClient(new StubHandler(content: null)));

        var version = await service.GetLatestVersionOrFallbackAsync("Contoso.Package.Offline", new Version(9, 9, 9), TestContext.Current.CancellationToken);

        Assert.Equal(new Version(9, 9, 9), version);
    }

    [Fact]
    public void SourceGeneratorFallback_IsTheCliOwnVersion()
    {
        var cli = typeof(ApiOptionsHelper).Assembly.GetName().Version!;

        Assert.Equal(new Version(cli.Major, cli.Minor, cli.Build), Atc.Rest.Api.Generator.Cli.Defaults.PackageVersionDefaults.SourceGeneratorFallback);
    }

    [Fact]
    public async Task GetLatestVersionAsync_WithHtmlOk_ReturnsNullWithoutThrowing()
    {
        // A captive portal answers 200 with a login page.
        var service = CreateService("<html><body>Sign in</body></html>", "text/html");

        var version = await service.GetLatestVersionAsync("Contoso.Package.CaptivePortal", TestContext.Current.CancellationToken);

        Assert.Null(version);
    }

    [Fact]
    public async Task GetLatestVersionAsync_WithNonStringVersionEntry_ReturnsNullWithoutThrowing()
    {
        var service = CreateService("""{ "versions": [ 1, 2 ] }""");

        var version = await service.GetLatestVersionAsync("Contoso.Package.Numbers", TestContext.Current.CancellationToken);

        Assert.Null(version);
    }

    [Fact]
    public async Task GetLatestVersionAsync_WithServerError_ReturnsNullWithoutThrowing()
    {
        var service = new NugetPackageVersionService(new HttpClient(new StubHandler("boom", "text/plain", HttpStatusCode.InternalServerError)));

        var version = await service.GetLatestVersionAsync("Contoso.Package.ServerError", TestContext.Current.CancellationToken);

        Assert.Null(version);
    }

    [Fact]
    public async Task GetLatestVersionAsync_WithHandlerThatThrows_ReturnsNullWithoutThrowing()
    {
        var service = new NugetPackageVersionService(new HttpClient(new ThrowingHandler()));

        var version = await service.GetLatestVersionAsync("Contoso.Package.Throws", TestContext.Current.CancellationToken);

        Assert.Null(version);
    }

    [Fact]
    public async Task GetLatestVersionAsync_WithResponseThatNeverCompletes_ReturnsNullWithinBudget()
    {
        var service = new NugetPackageVersionService(new HttpClient(new HangingHandler()));
        using var cts = new CancellationTokenSource(Budget);
        var stopwatch = Stopwatch.StartNew();

        var version = await service.GetLatestVersionAsync("Contoso.Package.Hangs", cts.Token);

        stopwatch.Stop();
        Assert.Null(version);
        Assert.True(stopwatch.Elapsed < Budget + TimeSpan.FromSeconds(3), $"Took {stopwatch.Elapsed}.");
    }

    private static NugetPackageVersionService CreateService(
        string content,
        string mediaType = "application/json")
        => new(new HttpClient(new StubHandler(content, mediaType)));

    private sealed class StubHandler(
        string? content,
        string mediaType = "application/json",
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => content is null
                ? throw new HttpRequestException("offline")
                : Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(content, Encoding.UTF8, mediaType) });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("DNS is lying today.");
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}