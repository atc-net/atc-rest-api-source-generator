namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

public sealed class GenerateClientCommandSettingsTests : IDisposable
{
    private readonly SettingsFixture fixture = new();

    public void Dispose()
        => fixture.Dispose();

    [Theory]
    [InlineData("Streaming", null, null, "Invalid generation mode: 'Streaming'")]
    [InlineData(null, "PerTag", null, "Invalid client granularity: 'PerTag'")]
    [InlineData(null, null, "Demo Client", "Client project name cannot contain spaces")]
    public void Validate_RejectsInvalidCombination(
        string? generationMode,
        string? clientGranularity,
        string? clientProjectName,
        string expectedMessage)
    {
        var settings = new GenerateClientCommandSettings
        {
            SpecificationPath = fixture.SpecPath,
            OutputPath = Path.Combine(fixture.Root, "out"),
            ProjectName = "Demo.ApiClient",
            GenerationMode = generationMode,
            ClientGranularity = clientGranularity,
            ClientProjectName = clientProjectName,
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains(expectedMessage, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_PropagatesBaseErrors()
    {
        var settings = new GenerateClientCommandSettings
        {
            SpecificationPath = fixture.MissingPath,
            OutputPath = Path.Combine(fixture.Root, "out"),
            ProjectName = "Demo.ApiClient",
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("Specification file not found", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("TypedClient", "PerArea")]
    [InlineData("endpointperoperation", "single")]
    [InlineData(null, null)]
    public void Validate_AcceptsKnownModesAndGranularities(
        string? generationMode,
        string? clientGranularity)
    {
        var settings = new GenerateClientCommandSettings
        {
            SpecificationPath = fixture.SpecPath,
            OutputPath = Path.Combine(fixture.Root, "out"),
            ProjectName = "Demo.ApiClient",
            GenerationMode = generationMode,
            ClientGranularity = clientGranularity,
            ClientProjectName = "Demo.Client",
        };

        var result = settings.Validate();

        Assert.True(result.Successful, result.Message);
    }
}