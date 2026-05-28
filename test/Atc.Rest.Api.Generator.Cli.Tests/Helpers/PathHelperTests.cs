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
    public void PathHelper_ResolveRelativePath_StripsBinDebugTfmSegmentFromCwdPrefix()
    {
        // Scenario: the CLI is launched via `dotnet run` from inside a project's own
        // bin/<Config>/<tfm>/ output folder. CWD ends with that pattern; relative
        // arguments should resolve against the project root, not the bin folder.
        WithCwdLayout("src/MyProject/bin/Debug/net10.0", (tempRoot, _) =>
        {
            // Act
            var result = PathHelper.ResolveRelativePath("./specs/api.yaml");

            // Assert — both the bin/Debug/net10.0/ and the src/MyProject/ CWD segments
            // strip away, anchoring the resolution at the synthesized repo root.
            Assert.Equal(Path.Combine(tempRoot, "specs", "api.yaml"), result);
        });
    }

    [Theory]
    [InlineData("Release", "net9.0")]
    [InlineData("Debug", "net8.0")]
    [InlineData("Release", "net10.0")]
    public void PathHelper_ResolveRelativePath_StripsBinOutputCwdAcrossConfigurationsAndTfms(
        string configuration,
        string tfm)
    {
        WithCwdLayout($"src/MyProject/bin/{configuration}/{tfm}", (tempRoot, _) =>
        {
            // Act
            var result = PathHelper.ResolveRelativePath("./out.txt");

            // Assert
            Assert.Equal(Path.Combine(tempRoot, "out.txt"), result);
        });
    }

    [Fact]
    public void PathHelper_ResolveRelativePath_StripsSrcProjectFolderFromCwdPrefix()
    {
        // CWD ends with src/<ProjectName>/ but no bin segment — still strips the src/
        // hop so paths land at the repo root.
        WithCwdLayout("src/MyProject", (tempRoot, _) =>
        {
            // Act
            var result = PathHelper.ResolveRelativePath("./specs/api.yaml");

            // Assert
            Assert.Equal(Path.Combine(tempRoot, "specs", "api.yaml"), result);
        });
    }

    [Fact]
    public void PathHelper_ResolveRelativePath_DoesNotStripSegmentsTypedInTheArgument()
    {
        // Regression guard: a relative argument whose resolved path contains
        // src/<segment>/ must NOT have those segments stripped just because they
        // look like the CWD strip pattern. The strip is anchored to the CWD prefix only.
        WithCwdLayout("src/consumer-app/ClientApp", (tempRoot, _) =>
        {
            // Act
            var result = PathHelper.ResolveRelativePath(Path.Combine("..", "..", "api-spec", "api.yaml"));

            // Assert — the ../.. navigates out of CWD into a sibling under src/, and
            // that src/api-spec/ segment is the user's intent. It must survive.
            Assert.Equal(Path.Combine(tempRoot, "src", "api-spec", "api.yaml"), result);
        });
    }

    [Fact]
    public void PathHelper_ResolveRelativePath_DoesNotStripBinFolder_WhenTfmSegmentIsAbsent()
    {
        // The regex requires the full bin/<Config>/<tfm>/ shape, so a plain `bin/` segment
        // in CWD without a `net*` folder must survive unchanged.
        WithCwdLayout("project/bin", (_, cwd) =>
        {
            // Act
            var result = PathHelper.ResolveRelativePath("./api.yaml");

            // Assert
            Assert.Equal(Path.Combine(cwd, "api.yaml"), result);
        });
    }

    [Fact]
    public void PathHelper_ResolveRelativePath_LeavesPathAlone_WhenCwdHasNoStripPattern()
    {
        // A clean CWD (no bin/<Config>/<tfm> and no src/<X>) means the helper does no
        // rewriting — even if the user's typed argument contains shapes that would
        // otherwise match the strip patterns.
        WithTempCwd(tempDir =>
        {
            // Act
            var result = PathHelper.ResolveRelativePath(Path.Combine(".", "src", "MyProject", "specs", "api.yaml"));

            // Assert
            Assert.Equal(Path.Combine(tempDir, "src", "MyProject", "specs", "api.yaml"), result);
        });
    }

    /// <summary>
    /// Runs <paramref name="action"/> with the current working directory set to a temp
    /// folder whose tail matches <paramref name="cwdSuffix"/> (forward-slash separated).
    /// Passes back the synthesized repo root (the temp dir itself) and the actual CWD,
    /// so tests can assert against either anchor. Use this to exercise the CWD-prefix
    /// strip — tests that need a clean CWD without strippable segments should use
    /// <see cref="WithTempCwd"/> instead.
    /// </summary>
    private static void WithCwdLayout(
        string cwdSuffix,
        Action<string, string> action)
    {
        var originalCwd = Directory.GetCurrentDirectory();
        var tempRoot = Path.Combine(Path.GetTempPath(), "atc-path-helper-" + Guid.NewGuid().ToString("N"));
        var cwd = Path.Combine(tempRoot, cwdSuffix.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(cwd);
        try
        {
            Directory.SetCurrentDirectory(cwd);
            action(tempRoot, cwd);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best effort cleanup — not fatal to the test.
            }
        }
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