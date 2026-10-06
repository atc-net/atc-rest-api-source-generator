namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

public sealed class SpecValidateCommandSettingsTests : IDisposable
{
    private readonly SettingsFixture fixture = new();

    public void Dispose()
        => fixture.Dispose();

    [Theory]
    [InlineData("", null, "Specification path is required")]
    [InlineData("missing.yaml", null, "Specification file not found")]
    [InlineData("notes.txt", null, "must be a YAML")]
    [InlineData("missing.yaml", "api.yaml,api_Users.yaml", "Specification file not found")]
    public void Validate_RejectsInvalidCombination(
        string spec,
        string? files,
        string expectedMessage)
    {
        var settings = new SpecValidateCommandSettings
        {
            SpecificationPath = spec.Length == 0 ? string.Empty : Path.Combine(fixture.Root, spec),
            ExplicitFiles = files,
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains(expectedMessage, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsSpec()
    {
        var settings = new SpecValidateCommandSettings
        {
            SpecificationPath = fixture.SpecPath,
            DisableStrictMode = true,
        };

        var result = settings.Validate();

        Assert.True(result.Successful, result.Message);
        Assert.False(settings.StrictMode);
        Assert.False(settings.UseMultiPart);
    }

    [Fact]
    public void Validate_AcceptsExplicitFilesWithoutSpec_AndTurnsOnMultiPart()
    {
        var settings = new SpecValidateCommandSettings
        {
            ExplicitFiles = "api.yaml,api_Users.yaml",
        };

        var result = settings.Validate();

        Assert.True(result.Successful, result.Message);
        Assert.True(settings.UseMultiPart);
    }
}