namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

public sealed class SpecOverlayApplyCommandSettingsTests : IDisposable
{
    private readonly SettingsFixture fixture = new();

    public void Dispose()
        => fixture.Dispose();

    [Theory]
    [InlineData("", "fixes.overlay.yaml", "out.yaml", "Specification path is required")]
    [InlineData("missing.yaml", "fixes.overlay.yaml", "out.yaml", "Specification file not found")]
    [InlineData("api.yaml", "", "out.yaml", "At least one overlay is required")]
    [InlineData("api.yaml", "missing.overlay.yaml", "out.yaml", "Overlay file not found")]
    [InlineData("api.yaml", "fixes.overlay.yaml", "", "Output path is required")]
    public void Validate_RejectsInvalidCombination(
        string spec,
        string overlay,
        string output,
        string expectedMessage)
    {
        var settings = new SpecOverlayApplyCommandSettings
        {
            SpecificationPath = spec.Length == 0 ? string.Empty : Path.Combine(fixture.Root, spec),
            Overlays = overlay.Length == 0 ? [] : [Path.Combine(fixture.Root, overlay)],
            OutputPath = output.Length == 0 ? string.Empty : Path.Combine(fixture.Root, output),
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains(expectedMessage, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsSpecOverlayAndOutput_AndResolvesThePaths()
    {
        var settings = new SpecOverlayApplyCommandSettings
        {
            SpecificationPath = fixture.SpecPath,
            Overlays = [fixture.OverlayPath],
            OutputPath = Path.Combine("effective", "api.yaml"),
        };

        var result = settings.Validate();

        Assert.True(result.Successful, result.Message);
        Assert.Equal(fixture.SpecPath, settings.SpecificationPath);
        Assert.Equal(Path.Combine(Directory.GetCurrentDirectory(), "effective", "api.yaml"), settings.OutputPath);
    }
}