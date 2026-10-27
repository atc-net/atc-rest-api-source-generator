namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// The update check that runs before a command must stay invisible: no HTTP trace lines in the
/// output, and no network round trip at all for help and version.
/// </summary>
[Collection("Sequential-CLI")]
public sealed class UpdateCheckTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("--version")]
    [InlineData("-v")]
    [InlineData("")]
    public async Task HelpAndVersion_PrintNoHttpTrace(string arguments)
    {
        var (exitCode, output) = await CliTestHelper.RunCliAsync(arguments, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("HTTP request", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api.nuget.org", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Command_PrintsNoHttpTrace()
    {
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");

        var (exitCode, output) = await CliTestHelper.RunCliAsync(
            $"spec validate -s \"{yamlPath}\" --no-strict",
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("HTTP request", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api.nuget.org", output, StringComparison.OrdinalIgnoreCase);
    }
}