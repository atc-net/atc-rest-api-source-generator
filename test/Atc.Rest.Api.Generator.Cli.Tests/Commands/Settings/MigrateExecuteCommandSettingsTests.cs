namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

public sealed class MigrateExecuteCommandSettingsTests : IDisposable
{
    private readonly SettingsFixture fixture = new();

    public void Dispose()
        => fixture.Dispose();

    [Theory]
    [InlineData("", null, "sln", "Specification path is required")]
    [InlineData("missing.yaml", null, "sln", "Specification file not found")]
    [InlineData("notes.txt", null, "sln", "must be a YAML")]
    [InlineData("api.yaml", "api.yaml", "sln", "given twice")]
    [InlineData("api.yaml", null, "missing-sln", "Solution path not found")]
    [InlineData("api.yaml", null, "notes.txt", "must be a .sln or .slnx")]
    public void Validate_RejectsInvalidCombination(
        string spec,
        string? legacySpec,
        string solution,
        string expectedMessage)
    {
        var settings = new MigrateExecuteCommandSettings
        {
            SpecificationPath = spec.Length == 0 ? string.Empty : Path.Combine(fixture.Root, spec),
            LegacySpecificationPath = legacySpec is null ? null : Path.Combine(fixture.Root, legacySpec),
            SolutionPath = Path.Combine(fixture.Root, solution),
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains(expectedMessage, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsSpecAndSolutionDirectory()
    {
        var settings = new MigrateExecuteCommandSettings
        {
            SpecificationPath = fixture.SpecPath,
            SolutionPath = fixture.SolutionDirectory,
            DryRun = true,
        };

        var result = settings.Validate();

        Assert.True(result.Successful, result.Message);
        Assert.Equal(fixture.SolutionDirectory, settings.SolutionPath);
    }

    [Fact]
    public void Validate_AcceptsLegacySpelling_AndNotesIt()
    {
        // The pre-2.1.1 spelling: -s <solution> -p <specification>.
        var settings = new MigrateExecuteCommandSettings
        {
            SpecificationPath = fixture.SolutionPath,
            LegacySpecificationPath = fixture.SpecPath,
        };

        var result = settings.Validate();

        Assert.True(result.Successful, result.Message);
        Assert.Equal(fixture.SpecPath, settings.SpecificationPath);
        Assert.Equal(fixture.SolutionPath, settings.SolutionPath);
        Assert.Contains(settings.PathNotes, note => note.Contains("--solution", StringComparison.Ordinal));
    }
}