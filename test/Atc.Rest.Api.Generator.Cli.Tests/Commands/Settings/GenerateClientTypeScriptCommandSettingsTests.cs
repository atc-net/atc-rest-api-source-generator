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

    // A number is no value of the option, though Enum.TryParse takes it
    [InlineData("1", null, null, null, null, "Invalid enum style: '1'")]
    [InlineData(null, "7", null, null, null, "Invalid hooks style: '7'")]
    [InlineData(null, "ReactQuery", "2", null, null, "Invalid hooks mode: '2'")]
    [InlineData(null, null, null, "-1", null, "Invalid client type: '-1'")]
    [InlineData(null, null, null, null, "0", "Invalid naming strategy: '0'")]

    // The hooks mode chooses between React Query's hooks: with SWR, no hooks or no --hooks it would be ignored
    [InlineData(null, "Swr", "Suspense", null, null, "--hooks-mode applies to --hooks ReactQuery only")]
    [InlineData(null, "None", "Both", null, null, "--hooks-mode applies to --hooks ReactQuery only")]
    [InlineData(null, null, "Standard", null, null, "--hooks-mode applies to --hooks ReactQuery only")]
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
    public void Validate_NamesEveryValueOfTheOption()
    {
        var settings = new GenerateClientTypeScriptCommandSettings
        {
            SpecificationPath = fixture.SpecPath,
            OutputPath = Path.Combine(fixture.Root, "out"),
            HooksStyle = "Redux",
        };

        var result = settings.Validate();

        Assert.Contains("Valid values: " + string.Join(", ", Enum.GetNames<TypeScriptHooksStyle>()) + ".", result.Message, StringComparison.Ordinal);
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
    [InlineData("enum", "swr", null, "axios", "pascalcase")]
    [InlineData("union", "reactquery", "both", "fetch", "original")]
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