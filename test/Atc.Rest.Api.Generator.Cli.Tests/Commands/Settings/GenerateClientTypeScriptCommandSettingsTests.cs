namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

public sealed class GenerateClientTypeScriptCommandSettingsTests : IDisposable
{
    private readonly SettingsFixture fixture = new();

    public void Dispose()
        => fixture.Dispose();

    [Theory]
    [InlineData("", "out", "Specification path is required")]
    [InlineData("missing.yaml", "out", "Specification file not found")]
    [InlineData("notes.txt", "out", "must be a YAML")]
    [InlineData("api.yaml", "", "Output path is required")]
    public void Validate_RejectsMissingOrWrongPaths(
        string spec,
        string output,
        string expectedMessage)
    {
        var settings = new GenerateClientTypeScriptCommandSettings
        {
            SpecificationPath = spec.Length == 0 ? string.Empty : Path.Combine(fixture.Root, spec),
            OutputPath = output.Length == 0 ? string.Empty : Path.Combine(fixture.Root, output),
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains(expectedMessage, result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Flags", null, null, null, null, "Invalid enum style: 'Flags'")]
    [InlineData(null, "Redux", null, null, null, "Invalid hooks style: 'Redux'")]
    [InlineData(null, null, "Lazy", null, null, "Invalid hooks mode: 'Lazy'")]
    [InlineData(null, null, null, "Ky", null, "Invalid client type: 'Ky'")]
    [InlineData(null, null, null, null, "SnakeCase", "Invalid naming strategy: 'SnakeCase'")]
    public void Validate_RejectsInvalidCombination(
        string? enumStyle,
        string? hooksStyle,
        string? hooksMode,
        string? clientType,
        string? namingStrategy,
        string expectedMessage)
    {
        var settings = new GenerateClientTypeScriptCommandSettings
        {
            SpecificationPath = fixture.SpecPath,
            OutputPath = Path.Combine(fixture.Root, "out"),
            EnumStyle = enumStyle,
            HooksStyle = hooksStyle,
            HooksMode = hooksMode,
            ClientType = clientType,
            NamingStrategy = namingStrategy,
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains(expectedMessage, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsMissingOverlay()
    {
        var settings = new GenerateClientTypeScriptCommandSettings
        {
            SpecificationPath = fixture.SpecPath,
            OutputPath = Path.Combine(fixture.Root, "out"),
            Overlays = [fixture.MissingOverlayPath],
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("Overlay file not found", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Union", "ReactQuery", "Suspense", "Fetch", "CamelCase")]
    [InlineData("enum", "swr", "both", "axios", "pascalcase")]
    [InlineData(null, null, null, null, null)]
    public void Validate_AcceptsKnownValues(
        string? enumStyle,
        string? hooksStyle,
        string? hooksMode,
        string? clientType,
        string? namingStrategy)
    {
        var settings = new GenerateClientTypeScriptCommandSettings
        {
            SpecificationPath = fixture.SpecPath,
            OutputPath = Path.Combine(fixture.Root, "out"),
            EnumStyle = enumStyle,
            HooksStyle = hooksStyle,
            HooksMode = hooksMode,
            ClientType = clientType,
            NamingStrategy = namingStrategy,
            Overlays = [fixture.OverlayPath],
        };

        var result = settings.Validate();

        Assert.True(result.Successful, result.Message);
        Assert.True(Path.IsPathRooted(settings.OutputPath));
    }
}