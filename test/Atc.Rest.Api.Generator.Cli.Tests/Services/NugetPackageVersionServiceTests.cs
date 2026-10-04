namespace Atc.Rest.Api.Generator.Cli.Tests.Services;

[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The HttpClient owns the stub handler; both live for the test only.")]
public sealed class NugetPackageVersionServiceTests
{
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

    private static NugetPackageVersionService CreateService(string content)
        => new(new HttpClient(new StubHandler(content)));

    private sealed class StubHandler(string? content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => content is null
                ? throw new HttpRequestException("offline")
                : Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(content) });
    }
}