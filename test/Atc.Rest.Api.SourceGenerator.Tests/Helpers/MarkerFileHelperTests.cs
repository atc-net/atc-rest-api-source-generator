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
        // Arrange
        var dir = Path.Combine(tempRoot, "project");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, ".atc-rest-api-server"),
            "{\"namespace\":\"MyApp.Api\"}");

        // Act
        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        // Assert
        Assert.Equal("MyApp.Api", result);
    }

    [Fact]
    public void TryGetServerNamespace_JsonMarkerVariant_FindsMarker()
    {
        // Arrange
        var dir = Path.Combine(tempRoot, "project");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, ".atc-rest-api-server.json"),
            "{\"namespace\":\"MyApp.Api.Json\"}");

        // Act
        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        // Assert
        Assert.Equal("MyApp.Api.Json", result);
    }

    [Fact]
    public void TryGetServerNamespace_SiblingDirectory_FindsMarker()
    {
        // Arrange - Create parent with two sibling directories
        var parent = Path.Combine(tempRoot, "parent");
        var domainDir = Path.Combine(parent, "Domain");
        var serverDir = Path.Combine(parent, "Server");
        Directory.CreateDirectory(domainDir);
        Directory.CreateDirectory(serverDir);

        File.WriteAllText(
            Path.Combine(serverDir, ".atc-rest-api-server"),
            "{\"namespace\":\"MyApp.Api.Sibling\"}");

        // Act
        var result = MarkerFileHelper.TryGetServerNamespace(domainDir);

        // Assert
        Assert.Equal("MyApp.Api.Sibling", result);
    }

    [Fact]
    public void TryGetServerNamespace_MultipleContractsSiblings_PrefersConventionMatch()
    {
        // Arrange - Multi-API repo: parent has multiple *.Contracts siblings.
        // Domain "MyApp.GithubContextAnd.Api.Domain" must pair with sibling
        // "MyApp.GithubContextAnd.Api.Contracts", NOT alphabetically-first "MyApp.Api.Contracts".
        var parent = Path.Combine(tempRoot, "multi");
        var domainDir = Path.Combine(parent, "MyApp.GithubContextAnd.Api.Domain");
        var firstContractsDir = Path.Combine(parent, "MyApp.Api.Contracts");
        var matchingContractsDir = Path.Combine(parent, "MyApp.GithubContextAnd.Api.Contracts");
        var anotherContractsDir = Path.Combine(parent, "MyApp.GithubGateway.Api.Contracts");

        Directory.CreateDirectory(domainDir);
        Directory.CreateDirectory(firstContractsDir);
        Directory.CreateDirectory(matchingContractsDir);
        Directory.CreateDirectory(anotherContractsDir);

        File.WriteAllText(
            Path.Combine(firstContractsDir, ".atc-rest-api-server"),
            "{\"namespace\":\"MyApp.Api.Contracts\"}");
        File.WriteAllText(
            Path.Combine(matchingContractsDir, ".atc-rest-api-server"),
            "{\"namespace\":\"MyApp.GithubContextAnd.Api.Contracts\"}");
        File.WriteAllText(
            Path.Combine(anotherContractsDir, ".atc-rest-api-server"),
            "{\"namespace\":\"MyApp.GithubGateway.Api.Contracts\"}");

        // Act
        var result = MarkerFileHelper.TryGetServerNamespace(domainDir);

        // Assert - should pick the convention-matching sibling, not the alphabetical first
        Assert.Equal("MyApp.GithubContextAnd.Api.Contracts", result);
    }

    [Fact]
    public void TryGetServerNamespace_MultipleContractsSiblings_JsonVariant_PrefersConventionMatch()
    {
        // Arrange - Same as above but using .json marker variant on the matching sibling
        var parent = Path.Combine(tempRoot, "multi-json");
        var domainDir = Path.Combine(parent, "MyApp.GithubContextAnd.Api.Domain");
        var firstContractsDir = Path.Combine(parent, "MyApp.Api.Contracts");
        var matchingContractsDir = Path.Combine(parent, "MyApp.GithubContextAnd.Api.Contracts");

        Directory.CreateDirectory(domainDir);
        Directory.CreateDirectory(firstContractsDir);
        Directory.CreateDirectory(matchingContractsDir);

        File.WriteAllText(
            Path.Combine(firstContractsDir, ".atc-rest-api-server"),
            "{\"namespace\":\"MyApp.Api.Contracts\"}");
        File.WriteAllText(
            Path.Combine(matchingContractsDir, ".atc-rest-api-server.json"),
            "{\"namespace\":\"MyApp.GithubContextAnd.Api.Contracts\"}");

        // Act
        var result = MarkerFileHelper.TryGetServerNamespace(domainDir);

        // Assert
        Assert.Equal("MyApp.GithubContextAnd.Api.Contracts", result);
    }

    [Fact]
    public void TryGetServerNamespace_NoMarker_ReturnsNull()
    {
        // Arrange
        var dir = Path.Combine(tempRoot, "empty");
        Directory.CreateDirectory(dir);

        // Act
        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGetServerNamespace_InvalidJson_ReturnsNull()
    {
        // Arrange
        var dir = Path.Combine(tempRoot, "invalid");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, ".atc-rest-api-server"),
            "not valid json {{{");

        // Act
        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGetServerNamespace_EmptyDirectory_ReturnsNull()
    {
        // Arrange
        var dir = Path.Combine(tempRoot, "emptydir");
        Directory.CreateDirectory(dir);

        // Act
        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGetServerNamespace_EmptyString_ReturnsNull()
    {
        // Act
        var result = MarkerFileHelper.TryGetServerNamespace(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGetServerNamespace_MultipleContractsSiblings_NoConventionMatch_ReturnsNullForGEN011()
    {
        // Arrange — multiple sibling markers exist, but the Domain dir doesn't follow
        // the <X>.Domain ↔ <X>.Contracts convention, so auto-detect must give up rather
        // than silently pick the wrong sibling. Caller is expected to emit ATC_API_GEN011.
        var parent = Path.Combine(tempRoot, "ambiguous");
        var domainDir = Path.Combine(parent, "WeirdName.Service.Handlers");
        var contracts1 = Path.Combine(parent, "MyApp.Api.Contracts");
        var contracts2 = Path.Combine(parent, "MyApp.Other.Api.Contracts");

        Directory.CreateDirectory(domainDir);
        Directory.CreateDirectory(contracts1);
        Directory.CreateDirectory(contracts2);

        File.WriteAllText(
            Path.Combine(contracts1, ".atc-rest-api-server"),
            "{\"namespace\":\"MyApp.Api.Contracts\"}");
        File.WriteAllText(
            Path.Combine(contracts2, ".atc-rest-api-server"),
            "{\"namespace\":\"MyApp.Other.Api.Contracts\"}");

        // Act
        var result = MarkerFileHelper.TryGetServerNamespace(domainDir);

        // Assert — convention match fails AND multiple siblings have markers → return null
        Assert.Null(result);
    }

    [Fact]
    public void HasMultipleSiblingServerMarkers_OneMarker_ReturnsFalse()
    {
        var parent = Path.Combine(tempRoot, "single");
        var domainDir = Path.Combine(parent, "Foo.Domain");
        var contractsDir = Path.Combine(parent, "Foo.Contracts");
        Directory.CreateDirectory(domainDir);
        Directory.CreateDirectory(contractsDir);
        File.WriteAllText(
            Path.Combine(contractsDir, ".atc-rest-api-server"),
            "{\"namespace\":\"Foo.Contracts\"}");

        Assert.False(MarkerFileHelper.HasMultipleSiblingServerMarkers(domainDir));
    }

    [Fact]
    public void HasMultipleSiblingServerMarkers_TwoMarkers_ReturnsTrue()
    {
        var parent = Path.Combine(tempRoot, "two");
        var domainDir = Path.Combine(parent, "Foo.Domain");
        var contracts1 = Path.Combine(parent, "Foo.Contracts");
        var contracts2 = Path.Combine(parent, "Bar.Contracts");
        Directory.CreateDirectory(domainDir);
        Directory.CreateDirectory(contracts1);
        Directory.CreateDirectory(contracts2);
        File.WriteAllText(
            Path.Combine(contracts1, ".atc-rest-api-server"),
            "{\"namespace\":\"Foo.Contracts\"}");
        File.WriteAllText(
            Path.Combine(contracts2, ".atc-rest-api-server"),
            "{\"namespace\":\"Bar.Contracts\"}");

        Assert.True(MarkerFileHelper.HasMultipleSiblingServerMarkers(domainDir));
    }

    [Fact]
    public void HasMultipleSiblingServerMarkers_NoMarkers_ReturnsFalse()
    {
        var parent = Path.Combine(tempRoot, "none");
        var domainDir = Path.Combine(parent, "Foo.Domain");
        var siblingDir = Path.Combine(parent, "Foo.Other");
        Directory.CreateDirectory(domainDir);
        Directory.CreateDirectory(siblingDir);

        Assert.False(MarkerFileHelper.HasMultipleSiblingServerMarkers(domainDir));
    }

    [Fact]
    public void TryGetServerNamespace_NoNamespaceInConfig_ReturnsNull()
    {
        // Arrange
        var dir = Path.Combine(tempRoot, "nonamespace");
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, ".atc-rest-api-server"),
            "{}");

        // Act
        var result = MarkerFileHelper.TryGetServerNamespace(dir);

        // Assert
        Assert.Null(result);
    }
}