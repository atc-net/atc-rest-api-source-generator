namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

public class GenerateServerCommandSettingsTests
{
    [Fact]
    public void Validate_UnknownVersioningStrategy_ListsExactlyTheStrategiesThatExist()
    {
        var settings = new GenerateServerCommandSettings
        {
            SpecificationPath = CliTestHelper.GetScenarioYamlPath("Demo"),
            OutputPath = Path.Combine(Path.GetTempPath(), "atc-settings-" + Guid.NewGuid().ToString("N")),
            ProjectName = "Contoso",
            VersioningStrategy = "Combined",
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Equal(
            $"Invalid versioning strategy: 'Combined'. Valid values: {string.Join(", ", Enum.GetNames<VersioningStrategyType>())}.",
            result.Message);
    }
}