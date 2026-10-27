namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Every TypeScript gate and runtime matrix takes its npm packages from one workspace, installed once per machine
/// from the committed lockfile (<c>test/NodeWorkspace</c>): a test run never asks the registry for the newest
/// version of anything, and twenty tests starting at once share one install instead of racing one npm cache.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class NodeWorkspaceTests
{
    [Fact]
    public void Link_GivesTheWorkDirectoryTheSharedPackages_OnceForEveryCaller()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            Assert.Skip(skipReason);
        }

        var first = Path.Combine(Path.GetTempPath(), "atc-node-workspace-test", Guid.NewGuid().ToString("N"));
        var second = Path.Combine(Path.GetTempPath(), "atc-node-workspace-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        try
        {
            var shared = NodeWorkspace.Link(first);
            var stopwatch = Stopwatch.StartNew();
            var sharedAgain = NodeWorkspace.Link(second);
            stopwatch.Stop();

            // The same install, linked into both; the second link does no install (seconds, not minutes)
            Assert.Equal(shared, sharedAgain);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"the second link took {stopwatch.Elapsed}");
            foreach (var directory in new[] { first, second })
            {
                Assert.True(File.Exists(Path.Combine(directory, "node_modules", "typescript", "package.json")), $"no typescript under {directory}");
                Assert.True(File.Exists(Path.Combine(directory, "node_modules", "tsx", "package.json")), $"no tsx under {directory}");
                Assert.True(File.Exists(Path.Combine(directory, "node_modules", "axios", "package.json")), $"no axios under {directory}");
            }

            // The versions are the lockfile's, not the registry's newest
            var lockfile = JsonNode.Parse(File.ReadAllText(Path.Combine(NodeWorkspace.SourceDirectory, "package-lock.json")))!;
            var pinnedTypeScript = (string?)lockfile["packages"]!["node_modules/typescript"]!["version"];
            var installedTypeScript = (string?)JsonNode.Parse(File.ReadAllText(Path.Combine(first, "node_modules", "typescript", "package.json")))!["version"];
            Assert.Equal(pinnedTypeScript, installedTypeScript);
        }
        finally
        {
            NodeWorkspace.Unlink(first);
            NodeWorkspace.Unlink(second);
            Directory.Delete(first, recursive: true);
            Directory.Delete(second, recursive: true);
        }
    }
}