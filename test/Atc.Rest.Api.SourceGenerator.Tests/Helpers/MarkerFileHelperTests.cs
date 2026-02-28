namespace Atc.Rest.Api.SourceGenerator.Tests.Helpers;

[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
public sealed class MarkerFileHelperTests : IDisposable
{
    private readonly string tempRoot;

    public MarkerFileHelperTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "MarkerFileHelperTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // ========== TryGetServerNamespace Tests ==========

    [Fact]
    public void TryGetServerNamespace_SameDirectory_FindsMarker()
    {
        var dir = Path.Combine(tempRoot, "project");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, ".atc-rest-api-server"),
            "{\"namespace\":\"MyApp.Api\"}");

        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        Assert.Equal("MyApp.Api", result);
    }

    [Fact]
    public void TryGetServerNamespace_JsonMarkerVariant_FindsMarker()
    {
        var dir = Path.Combine(tempRoot, "project");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, ".atc-rest-api-server.json"),
            "{\"namespace\":\"MyApp.Api.Json\"}");

        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        Assert.Equal("MyApp.Api.Json", result);
    }

    [Fact]
    public void TryGetServerNamespace_SiblingDirectory_FindsMarker()
    {
        // Create parent with two sibling directories
        var parent = Path.Combine(tempRoot, "parent");
        var domainDir = Path.Combine(parent, "Domain");
        var serverDir = Path.Combine(parent, "Server");
        Directory.CreateDirectory(domainDir);
        Directory.CreateDirectory(serverDir);

        File.WriteAllText(
            Path.Combine(serverDir, ".atc-rest-api-server"),
            "{\"namespace\":\"MyApp.Api.Sibling\"}");

        var result = MarkerFileHelper.TryGetServerNamespace(domainDir);

        Assert.Equal("MyApp.Api.Sibling", result);
    }

    [Fact]
    public void TryGetServerNamespace_NoMarker_ReturnsNull()
    {
        var dir = Path.Combine(tempRoot, "empty");
        Directory.CreateDirectory(dir);

        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetServerNamespace_InvalidJson_ReturnsNull()
    {
        var dir = Path.Combine(tempRoot, "invalid");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, ".atc-rest-api-server"),
            "not valid json {{{");

        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetServerNamespace_EmptyDirectory_ReturnsNull()
    {
        var dir = Path.Combine(tempRoot, "emptydir");
        Directory.CreateDirectory(dir);

        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetServerNamespace_EmptyString_ReturnsNull()
    {
        var result = MarkerFileHelper.TryGetServerNamespace(string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetServerNamespace_NoNamespaceInConfig_ReturnsNull()
    {
        var dir = Path.Combine(tempRoot, "nonamespace");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, ".atc-rest-api-server"),
            "{}");

        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        Assert.Null(result);
    }
}