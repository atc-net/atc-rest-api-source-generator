namespace Atc.Rest.Api.Generator.Cli.Tests.Helpers;

/// <summary>
/// The <c>--watch</c> watcher sees a specification saved by an editor that writes a temporary file and renames it
/// over the specification (rename-save), as well as an in-place write, and a part file of a multi-part set.
/// </summary>
public sealed class SpecificationWatcherTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atc-spec-watcher-" + Guid.NewGuid().ToString("N"));

    public SpecificationWatcherTests()
        => Directory.CreateDirectory(directory);

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("rename")]
    [InlineData("write")]
    [InlineData("part")]
    [InlineData("part-yml")]
    public async Task Watcher_SeesARenameSave_AWrite_AndAPartFile(string how)
    {
        var specPath = Path.Combine(directory, "Api.yaml");
        await File.WriteAllTextAsync(specPath, "openapi: 3.0.3\n", TestContext.Current.CancellationToken);
        using var seen = new SemaphoreSlim(0);
        using var watcher = new SpecificationWatcher(specPath, () => seen.Release());

        switch (how)
        {
            case "rename":
                var temp = Path.Combine(directory, "Api.yaml.tmp");
                await File.WriteAllTextAsync(temp, "openapi: 3.0.3\ninfo: {}\n", TestContext.Current.CancellationToken);
                File.Move(temp, specPath, overwrite: true);
                break;
            case "write":
                await File.WriteAllTextAsync(specPath, "openapi: 3.0.3\ninfo: {}\n", TestContext.Current.CancellationToken);
                break;
            case "part-yml":
                // A part with another specification extension than the base, as the build reads it
                await File.WriteAllTextAsync(Path.Combine(directory, "Api_Orders.yml"), "openapi: 3.0.3\n", TestContext.Current.CancellationToken);
                break;
            default:
                await File.WriteAllTextAsync(Path.Combine(directory, "Api_Users.yaml"), "openapi: 3.0.3\n", TestContext.Current.CancellationToken);
                break;
        }

        Assert.True(await seen.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken), $"the watcher did not see the {how}");
    }
}