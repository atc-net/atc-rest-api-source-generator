namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

public sealed class BaseGenerateCommandSettingsTests : IDisposable
{
    private readonly SettingsFixture fixture = new();

    public void Dispose()
        => fixture.Dispose();

    [Theory]
    [InlineData("", "out", "Demo", null, "Specification path is required")]
    [InlineData("missing.yaml", "out", "Demo", null, "Specification file not found")]
    [InlineData("notes.txt", "out", "Demo", null, "must be a YAML")]
    [InlineData("api.yaml", "", "Demo", null, "Output path is required")]
    [InlineData("api.yaml", "out", "", null, "Project name is required")]
    [InlineData("api.yaml", "out", "Demo Api", null, "cannot contain spaces")]
    [InlineData("api.yaml", "out", "Demo", "missing-options.json", "Options file or directory not found")]
    public void Validate_RejectsInvalidCombination(
        string spec,
        string output,
        string projectName,
        string? options,
        string expectedMessage)
    {
        var settings = new BaseGenerateCommandSettings
        {
            SpecificationPath = spec.Length == 0 ? string.Empty : Path.Combine(fixture.Root, spec),
            OutputPath = output.Length == 0 ? string.Empty : Path.Combine(fixture.Root, output),
            ProjectName = projectName,
            OptionsPath = options is null ? null : Path.Combine(fixture.Root, options),
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains(expectedMessage, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsMissingOverlay_BeforeAnythingElse()
    {
        var settings = new BaseGenerateCommandSettings
        {
            SpecificationPath = fixture.MissingPath,
            Overlays = [fixture.MissingOverlayPath],
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("Overlay file not found", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsSpecOutputAndName_AndResolvesThePaths()
    {
        var settings = new BaseGenerateCommandSettings
        {
            SpecificationPath = fixture.SpecPath,
            OutputPath = Path.Combine(fixture.Root, "out"),
            ProjectName = "Demo.Api.Contracts",
            OptionsPath = fixture.OptionsPath,
            Overlays = [fixture.OverlayPath],
        };

        var result = settings.Validate();

        Assert.True(result.Successful, result.Message);
        Assert.True(Path.IsPathRooted(settings.SpecificationPath));
        Assert.True(Path.IsPathRooted(settings.OutputPath));
        Assert.True(Path.IsPathRooted(settings.OptionsPath));
    }
}