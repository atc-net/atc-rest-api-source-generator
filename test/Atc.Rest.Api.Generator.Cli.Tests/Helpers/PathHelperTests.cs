namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

/// <summary>
/// A relative argument resolves against the current directory and nothing else: the
/// helper must not rewrite <c>src/&lt;Project&gt;/</c> or <c>bin/&lt;Config&gt;/&lt;tfm&gt;/</c>
/// segments of the current directory, and <c>api.yaml</c> must mean the same as <c>./api.yaml</c>.
/// </summary>
[Collection("PathHelperSerial")]
public class PathHelperTests
{
    [Theory]
    [InlineData(@"C:\absolute\windows\path")]
    [InlineData("D:/mixed/separators/path")]
    public void ResolveRelativePath_ReturnsAbsolutePathNormalized(string path)
    {
        var result = PathHelper.ResolveRelativePath(path);

        // A backslash is a separator on every platform
        Assert.Equal(Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar)), result);
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void ResolveRelativePath_ResolvesDotSlashAgainstCwd()
    {
        WithCwdLayout("plain", (_, cwd) =>
        {
            var result = PathHelper.ResolveRelativePath("./some-file");

            Assert.Equal(Path.Combine(cwd, "some-file"), result);
        });
    }

    [Theory]
    [InlineData("api.yaml")]
    [InlineData("./api.yaml")]
    [InlineData(".\\api.yaml")]
    [InlineData(".\\specs\\..\\api.yaml")]
    public void ResolveRelativePath_WithAndWithoutLeadingDot_ResolveToTheSameFile(
        string argument)
    {
        WithCwdLayout("plain", (_, cwd) =>
        {
            var result = PathHelper.ResolveRelativePath(argument);

            Assert.Equal(Path.Combine(cwd, "api.yaml"), result);
        });
    }

    [Fact]
    public void ResolveRelativePath_FromSrcProjectFolder_ResolvesAgainstThatFolder()
    {
        // Running from C:\repo\src\MyApi with -s ./api.yaml must mean C:\repo\src\MyApi\api.yaml,
        // never C:\repo\api.yaml.
        WithCwdLayout("src/MyApi", (_, cwd) =>
        {
            var result = PathHelper.ResolveRelativePath("./api.yaml");

            Assert.Equal(Path.Combine(cwd, "api.yaml"), result);
        });
    }

    [Fact]
    public void ResolveRelativePath_WithSrcSegmentInTheMiddleOfCwd_KeepsCwd()
    {
        // D:\src\work\repo must not become D:\repo.
        WithCwdLayout("src/work/repo", (_, cwd) =>
        {
            var result = PathHelper.ResolveRelativePath("./api.yaml");

            Assert.Equal(Path.Combine(cwd, "api.yaml"), result);
        });
    }

    [Theory]
    [InlineData("src/MyProject/bin/Debug/net10.0")]
    [InlineData("src/MyProject/bin/Release/net9.0")]
    [InlineData("project/bin")]
    public void ResolveRelativePath_FromBuildOutputFolder_ResolvesAgainstThatFolder(
        string cwdSuffix)
    {
        WithCwdLayout(cwdSuffix, (_, cwd) =>
        {
            var result = PathHelper.ResolveRelativePath("./specs/api.yaml");

            Assert.Equal(Path.Combine(cwd, "specs", "api.yaml"), result);
        });
    }

    [Fact]
    public void ResolveRelativePath_NavigatingUpOutOfCwd_KeepsTheSegmentsTyped()
    {
        WithCwdLayout("src/consumer-app/ClientApp", (tempRoot, _) =>
        {
            var result = PathHelper.ResolveRelativePath(Path.Combine("..", "..", "api-spec", "api.yaml"));

            Assert.Equal(Path.Combine(tempRoot, "src", "api-spec", "api.yaml"), result);
        });
    }

    [Fact]
    public void ResolveRelativePath_WithSrcProjectSegmentsInTheArgument_KeepsThem()
    {
        WithCwdLayout("plain", (_, cwd) =>
        {
            var result = PathHelper.ResolveRelativePath(Path.Combine(".", "src", "MyProject", "specs", "api.yaml"));

            Assert.Equal(Path.Combine(cwd, "src", "MyProject", "specs", "api.yaml"), result);
        });
    }

    /// <summary>
    /// Runs <paramref name="action"/> with the current working directory set to a temp folder whose
    /// tail is <paramref name="cwdSuffix"/> (forward-slash separated), passing the temp root and the
    /// actual current directory.
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
}