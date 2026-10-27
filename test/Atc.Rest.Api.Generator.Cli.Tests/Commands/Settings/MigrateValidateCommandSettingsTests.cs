namespace Atc.Rest.Api.Generator.Cli.Tests.Commands.Settings;

public sealed class MigrateValidateCommandSettingsTests : IDisposable
{
    private readonly string tempPath = Path.Combine(Path.GetTempPath(), "atc-migrate-settings-" + Guid.NewGuid().ToString("N"));

    public MigrateValidateCommandSettingsTests()
        => Directory.CreateDirectory(tempPath);

    public void Dispose()
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }

    [Fact]
    public void GetSolutionDirectory_SolutionPathIsDirectory_ReturnsIt()
    {
        var settings = new MigrateValidateCommandSettings { SolutionPath = tempPath };

        Assert.Equal(tempPath, settings.GetSolutionDirectory());
    }

    [Fact]
    public void GetSolutionDirectory_SolutionPathIsFile_ReturnsItsDirectory()
    {
        var solutionFile = Path.Combine(tempPath, "Demo.slnx");
        File.WriteAllText(solutionFile, "<Solution />");
        var settings = new MigrateValidateCommandSettings { SolutionPath = solutionFile };

        Assert.Equal(tempPath, settings.GetSolutionDirectory());
    }
}