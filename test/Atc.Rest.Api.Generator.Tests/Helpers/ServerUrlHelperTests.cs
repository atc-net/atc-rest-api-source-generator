namespace Atc.Rest.Api.Generator.Tests.Helpers;

[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Test data")]
public class ServerUrlHelperTests
{
    // ========== ExtractPathFromServerUrl Tests ==========
    [Theory]
    [InlineData("/api/v1", "/api/v1")]
    [InlineData("/api/v1/", "/api/v1")] // trailing slash trimmed
    [InlineData("/", null)] // root only returns null
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("   ", null)] // whitespace only
    [InlineData("http://example.com/api/v1", "/api/v1")]
    [InlineData("http://example.com/api/v1/", "/api/v1")]
    [InlineData("http://example.com", null)] // no path
    [InlineData("http://example.com/", null)] // root only
    [InlineData("https://api.example.com/v2/pets", "/v2/pets")]
    [InlineData("{protocol}://api.example.com/v1", "/v1")] // variable protocol
    [InlineData("{protocol}://api.example.com/", null)] // variable protocol, root only
    [InlineData("{protocol}://api.example.com", null)] // variable protocol, no path
    public void ExtractPathFromServerUrl_ReturnsExpectedResult(
        string? serverUrl,
        string? expected)
    {
        // Act
        var result = ServerUrlHelper.ExtractPathFromServerUrl(serverUrl!);

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== GetServersBasePath Tests ==========
    [Fact]
    public void GetServersBasePath_NoServers_ReturnsNull()
    {
        // Arrange
        var doc = new OpenApiDocument();

        // Act
        var result = ServerUrlHelper.GetServersBasePath(doc);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetServersBasePath_EmptyServers_ReturnsNull()
    {
        // Arrange
        var doc = new OpenApiDocument
        {
            Servers = [],
        };

        // Act
        var result = ServerUrlHelper.GetServersBasePath(doc);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetServersBasePath_WithServerUrl_ReturnsPath()
    {
        // Arrange
        var doc = new OpenApiDocument
        {
            Servers = [new OpenApiServer { Url = "/api/v1" }],
        };

        // Act
        var result = ServerUrlHelper.GetServersBasePath(doc);

        // Assert
        Assert.Equal("/api/v1", result);
    }

    [Fact]
    public void GetServersBasePath_MultipleServers_UsesFirst()
    {
        // Arrange
        var doc = new OpenApiDocument
        {
            Servers =
            [
                new OpenApiServer { Url = "/api/v1" },
                new OpenApiServer { Url = "/api/v2" },
            ],
        };

        // Act
        var result = ServerUrlHelper.GetServersBasePath(doc);

        // Assert
        Assert.Equal("/api/v1", result);
    }

    // ========== ResolveServerVariables Tests ==========
    [Fact]
    public void ResolveServerVariables_NullVariables_ReturnsOriginal()
    {
        // Act
        var result = ServerUrlHelper.ResolveServerVariables("https://api.example.com/{basePath}", null);

        // Assert
        Assert.Equal("https://api.example.com/{basePath}", result);
    }

    [Fact]
    public void ResolveServerVariables_EmptyVariables_ReturnsOriginal()
    {
        // Arrange
        var variables = new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal);

        // Act
        var result = ServerUrlHelper.ResolveServerVariables(
            "https://api.example.com/{basePath}",
            variables);

        // Assert
        Assert.Equal("https://api.example.com/{basePath}", result);
    }

    [Fact]
    public void ResolveServerVariables_WithBasePath_ResolvesToDefault()
    {
        // Arrange
        var variables = new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal)
        {
            ["basePath"] = new() { Default = "v1" },
        };

        // Act
        var result = ServerUrlHelper.ResolveServerVariables(
            "https://api.example.com/{basePath}",
            variables);

        // Assert
        Assert.Equal("https://api.example.com/v1", result);
    }

    [Fact]
    public void ResolveServerVariables_MultipleVariables_ResolvesAll()
    {
        // Arrange
        var variables = new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal)
        {
            ["protocol"] = new() { Default = "https" },
            ["host"] = new() { Default = "api.example.com" },
            ["basePath"] = new() { Default = "v2" },
        };

        // Act
        var result = ServerUrlHelper.ResolveServerVariables(
            "{protocol}://{host}/{basePath}",
            variables);

        // Assert
        Assert.Equal("https://api.example.com/v2", result);
    }

    [Fact]
    public void ResolveServerVariables_VariableNotInUrl_Unchanged()
    {
        // Arrange
        var variables = new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal)
        {
            ["unused"] = new() { Default = "value" },
        };

        // Act
        var result = ServerUrlHelper.ResolveServerVariables(
            "https://api.example.com/v1",
            variables);

        // Assert
        Assert.Equal("https://api.example.com/v1", result);
    }

    [Fact]
    public void GetServersBasePath_WithVariables_ResolvesBeforeExtractingPath()
    {
        // Arrange
        var doc = new OpenApiDocument
        {
            Servers =
            [
                new OpenApiServer
                {
                    Url = "https://api.example.com/{basePath}",
                    Variables = new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal)
                    {
                        ["basePath"] = new() { Default = "api/v1" },
                    },
                },
            ],
        };

        // Act
        var result = ServerUrlHelper.GetServersBasePath(doc);

        // Assert
        Assert.Equal("/api/v1", result);
    }
}