namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

[Collection("PathHelperSerial")]
public class PathHelperTests
{
    [Theory]
    [InlineData("/absolute/unix/path")]
    [InlineData(@"C:\absolute\windows\path")]
    [InlineData("D:/mixed/separators/path")]
    [InlineData("relative-but-no-dot/path")]
    public void PathHelper_ResolveRelativePath_ReturnsInputVerbatim_WhenPathDoesNotStartWithDot(
        string path)
    {
        // Act
        var result = PathHelper.ResolveRelativePath(path);

        // Assert
        Assert.Equal(path, result);
    }

    [Fact]
    public void PathHelper_ResolveRelativePath_ResolvesDotSlashAgainstCleanCwd()
    {
        // Using a clean CWD without a bin/Debug/net*/ tail isolates the behavior
        // from the test host's own output directory.
        WithTempCwd(tempDir =>
        {
            // Act
            var result = PathHelper.ResolveRelativePath("./some-file");

            // Assert
            Assert.Equal(Path.Combine(tempDir, "some-file"), result);
        });
    }

    [Fact]
    public void PathHelper_ResolveRelativePath_StripsBinDebugTfmSegmentFromResolvedPath()
    {
        // The helper normalizes paths that land inside a `bin/<Config>/<tfm>/` build
        // output folder so tooling can report source-relative paths instead of the
        // ephemeral build output location.
        WithTempCwd(tempDir =>
        {
            // Arrange
            var sep = Path.DirectorySeparatorChar;

            // Act
            var result = PathHelper.ResolveRelativePath($"./bin{sep}Debug{sep}net10.0{sep}specs{sep}api.yaml");

            // Assert — the bin/Debug/net10.0/ segment must be gone, leaving specs/api.yaml.
            Assert.Equal(Path.Combine(tempDir, "specs", "api.yaml"), result);
        });
    }

    [Theory]
    [InlineData("Release", "net9.0")]
    [InlineData("Debug", "net8.0")]
    [InlineData("Release", "net10.0")]
    public void PathHelper_ResolveRelativePath_StripsBinOutputAcrossConfigurationsAndTfms(
        string configuration,
        string tfm)
    {
        WithTempCwd(tempDir =>
        {
            // Arrange
            var sep = Path.DirectorySeparatorChar;

            // Act
            var result = PathHelper.ResolveRelativePath($"./bin{sep}{configuration}{sep}{tfm}{sep}out.txt");

            // Assert
            Assert.Equal(Path.Combine(tempDir, "out.txt"), result);
        });
    }

    [Fact]
    public void PathHelper_ResolveRelativePath_StripsSrcProjectFolderFromResolvedPath()
    {
        WithTempCwd(tempDir =>
        {
            // Arrange
            var sep = Path.DirectorySeparatorChar;

            // Act
            var result = PathHelper.ResolveRelativePath($"./src{sep}MyProject{sep}specs{sep}api.yaml");

            // Assert
            Assert.Equal(Path.Combine(tempDir, "specs", "api.yaml"), result);
        });
    }

    [Fact]
    public void PathHelper_ResolveRelativePath_DoesNotStripBinFolder_WhenTfmSegmentIsAbsent()
    {
        // The regex requires the full bin/<Config>/<tfm>/ shape, so a plain `bin/` segment
        // without a `net*` folder must survive unchanged.
        WithTempCwd(tempDir =>
        {
            // Arrange
            var sep = Path.DirectorySeparatorChar;

            // Act
            var result = PathHelper.ResolveRelativePath($"./bin{sep}api.yaml");

            // Assert
            Assert.Equal(Path.Combine(tempDir, "bin", "api.yaml"), result);
        });
    }

    /// <summary>
    /// Runs <paramref name="action"/> with the current working directory temporarily
    /// set to a clean temp folder that does NOT contain `bin/&lt;Config&gt;/net*/` segments,
    /// ensuring <see cref="Path.GetFullPath(string)"/> expansion is deterministic.
    /// </summary>
    private static void WithTempCwd(Action<string> action)
    {
        var originalCwd = Directory.GetCurrentDirectory();
        var tempDir = Path.Combine(Path.GetTempPath(), "atc-path-helper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            Directory.SetCurrentDirectory(tempDir);
            action(tempDir);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
                // Best effort cleanup — not fatal to the test.
            }
        }
    }
}