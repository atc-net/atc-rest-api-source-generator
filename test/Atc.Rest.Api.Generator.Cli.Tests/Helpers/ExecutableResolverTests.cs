namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

public class ExecutableResolverTests
{
    [Fact]
    public void Resolve_AlreadyRootedPath_ReturnsUnchanged()
    {
        var rooted = OperatingSystem.IsWindows()
            ? @"C:\some\dir\tool.exe"
            : "/some/dir/tool";

        var result = ExecutableResolver.Resolve(rooted);

        Assert.Equal(rooted, result);
    }

    [Fact]
    public void Resolve_ExecutableOnPath_ReturnsAbsolutePathToExistingFile()
    {
        // "dotnet" must be on PATH for the test host itself to be running.
        var result = ExecutableResolver.Resolve("dotnet");

        Assert.True(Path.IsPathRooted(result), $"Expected a rooted path, got '{result}'");
        Assert.True(File.Exists(result), $"Resolved path '{result}' does not exist");
    }

    [Fact]
    public void Resolve_UnknownExecutable_ReturnsOriginalNameUnchanged()
    {
        const string unknown = "definitely-not-a-real-executable-xyz123";

        var result = ExecutableResolver.Resolve(unknown);

        Assert.Equal(unknown, result);
    }
}