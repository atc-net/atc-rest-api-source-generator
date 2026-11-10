namespace Atc.Rest.Api.Generator.Cli.Tests;

/// <summary>
/// The CLI's wiring of the start-up update check and the NuGet lookups; the check and the lookups themselves are
/// tested in Atc.
/// </summary>
public class ProgramTests
{
    [Fact]
    public void CreateUpdateCheckOptions_UsesTheToolPackageItsOptOutVariableAndATwoSecondBudget()
    {
        using var httpClient = new HttpClient();
        var client = new NugetFeedClient(httpClient);

        var options = Program.CreateUpdateCheckOptions(client);

        Assert.Equal("atc-rest-api-gen", options.PackageId);
        Assert.Equal("ATC_REST_API_GEN_NO_UPDATE_CHECK", options.OptOutEnvironmentVariable);
        Assert.Equal(TimeSpan.FromSeconds(2), options.Budget);
        Assert.Equal(CliHelper.GetCurrentVersion(), options.CurrentVersion);
        Assert.Same(client, options.NugetFeedClient);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("1", null, true)]
    [InlineData("true", null, true)]
    [InlineData("0", null, false)]
    [InlineData(null, "true", true)]
    [InlineData(null, "false", false)]
    public void UpdateCheck_IsTurnedOffByTheToolsVariableOrCi(
        string? noUpdateCheck,
        string? ci,
        bool expected)
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [Program.NoUpdateCheckVariable] = noUpdateCheck,
            ["CI"] = ci,
        };

        Assert.Equal(
            expected,
            CliUpdateCheckHelper.IsUpdateCheckDisabled(name => environment.GetValueOrDefault(name), Program.NoUpdateCheckVariable));
    }

    [Fact]
    public void ConfigureServices_RegistersTheNugetFeedClient()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Program.ConfigureServices(services);

        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        var client = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<INugetFeedClient>(provider);

        Assert.IsType<NugetFeedClient>(client);
    }
}