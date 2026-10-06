namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

public sealed class OptionsCommandSettingsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsMissingOutputPath(string outputPath)
    {
        var settings = new OptionsCommandSettings
        {
            OutputPath = outputPath,
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("Output path is required", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsOutputPath_AndResolvesIt()
    {
        var settings = new OptionsCommandSettings
        {
            OutputPath = Path.Combine("options", "ApiGeneratorOptions.json"),
            Force = true,
        };

        var result = settings.Validate();

        Assert.True(result.Successful, result.Message);
        Assert.Equal(
            Path.Combine(Directory.GetCurrentDirectory(), "options", "ApiGeneratorOptions.json"),
            settings.OutputPath);
    }
}