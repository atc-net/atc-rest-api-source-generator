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
        var result = ServerUrlHelper.ExtractPathFromServerUrl(serverUrl!);
        Assert.Equal(expected, result);
    }

    // ========== GetServersBasePath Tests ==========
    [Fact]
    public void GetServersBasePath_NoServers_ReturnsNull()
    {
        var doc = new OpenApiDocument();
        var result = ServerUrlHelper.GetServersBasePath(doc);
        Assert.Null(result);
    }

    [Fact]
    public void GetServersBasePath_EmptyServers_ReturnsNull()
    {
        var doc = new OpenApiDocument
        {
            Servers = [],
        };
        var result = ServerUrlHelper.GetServersBasePath(doc);
        Assert.Null(result);
    }

    [Fact]
    public void GetServersBasePath_WithServerUrl_ReturnsPath()
    {
        var doc = new OpenApiDocument
        {
            Servers = [new OpenApiServer { Url = "/api/v1" }],
        };
        var result = ServerUrlHelper.GetServersBasePath(doc);
        Assert.Equal("/api/v1", result);
    }

    [Fact]
    public void GetServersBasePath_MultipleServers_UsesFirst()
    {
        var doc = new OpenApiDocument
        {
            Servers =
            [
                new OpenApiServer { Url = "/api/v1" },
                new OpenApiServer { Url = "/api/v2" },
            ],
        };
        var result = ServerUrlHelper.GetServersBasePath(doc);
        Assert.Equal("/api/v1", result);
    }

    // ========== ResolveServerVariables Tests ==========
    [Fact]
    public void ResolveServerVariables_NullVariables_ReturnsOriginal()
    {
        var result = ServerUrlHelper.ResolveServerVariables("https://api.example.com/{basePath}", null);
        Assert.Equal("https://api.example.com/{basePath}", result);
    }

    [Fact]
    public void ResolveServerVariables_EmptyVariables_ReturnsOriginal()
    {
        var result = ServerUrlHelper.ResolveServerVariables(
            "https://api.example.com/{basePath}",
            new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal));
        Assert.Equal("https://api.example.com/{basePath}", result);
    }

    [Fact]
    public void ResolveServerVariables_WithBasePath_ResolvesToDefault()
    {
        var variables = new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal)
        {
            ["basePath"] = new() { Default = "v1" },
        };

        var result = ServerUrlHelper.ResolveServerVariables(
            "https://api.example.com/{basePath}",
            variables);

        Assert.Equal("https://api.example.com/v1", result);
    }

    [Fact]
    public void ResolveServerVariables_MultipleVariables_ResolvesAll()
    {
        var variables = new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal)
        {
            ["protocol"] = new() { Default = "https" },
            ["host"] = new() { Default = "api.example.com" },
            ["basePath"] = new() { Default = "v2" },
        };

        var result = ServerUrlHelper.ResolveServerVariables(
            "{protocol}://{host}/{basePath}",
            variables);

        Assert.Equal("https://api.example.com/v2", result);
    }

    [Fact]
    public void ResolveServerVariables_VariableNotInUrl_Unchanged()
    {
        var variables = new Dictionary<string, OpenApiServerVariable>(StringComparer.Ordinal)
        {
            ["unused"] = new() { Default = "value" },
        };

        var result = ServerUrlHelper.ResolveServerVariables(
            "https://api.example.com/v1",
            variables);

        Assert.Equal("https://api.example.com/v1", result);
    }

    [Fact]
    public void GetServersBasePath_WithVariables_ResolvesBeforeExtractingPath()
    {
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

        var result = ServerUrlHelper.GetServersBasePath(doc);
        Assert.Equal("/api/v1", result);
    }
}