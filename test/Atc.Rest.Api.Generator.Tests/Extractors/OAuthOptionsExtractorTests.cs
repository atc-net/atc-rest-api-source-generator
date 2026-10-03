namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OAuthOptionsExtractorTests
{
    [Fact]
    public void Extract_NullConfig_ReturnsNull()
    {
        Assert.Null(OAuthOptionsExtractor.Extract(null, "TestApi"));
    }

    [Fact]
    public void Extract_ClientCredentialsOnly_DefaultsTokenUrlAndScopesFromSpecWithoutRefreshUrl()
    {
        var config = new OAuthConfig
        {
            HasClientCredentials = true,
            ClientCredentialsTokenUrl = "https://auth.example.com/token",
        };
        config.DefaultScopes.Add("pets.read", "Read pets");

        var result = OAuthOptionsExtractor.Extract(config, "TestApi");

        Assert.NotNull(result);
        Assert.Contains("namespace TestApi.Generated.OAuth;", result, StringComparison.Ordinal);
        Assert.Contains("public class OAuthClientOptions", result, StringComparison.Ordinal);
        Assert.Contains("///   \"OAuth:TestApi\": {", result, StringComparison.Ordinal);

        Assert.Contains("/// Default from OpenAPI spec: https://auth.example.com/token", result, StringComparison.Ordinal);
        Assert.Contains("public string TokenUrl { get; set; } = \"https://auth.example.com/token\";", result, StringComparison.Ordinal);
        Assert.Contains("public string ClientId { get; set; } = string.Empty;", result, StringComparison.Ordinal);
        Assert.Contains("public string ClientSecret { get; set; } = string.Empty;", result, StringComparison.Ordinal);
        Assert.Contains("/// Default from OpenAPI spec: \"pets.read\"", result, StringComparison.Ordinal);
        Assert.Contains("public string Scopes { get; set; } = \"pets.read\";", result, StringComparison.Ordinal);
        Assert.Contains("public int RefreshBufferSeconds { get; set; } = 60;", result, StringComparison.Ordinal);

        // No Authorization Code refresh URL, so no RefreshUrl property.
        Assert.DoesNotContain("RefreshUrl", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_AuthorizationCodeWithRefreshUrl_EmitsRefreshUrlProperty()
    {
        var config = new OAuthConfig
        {
            HasAuthorizationCode = true,
            AuthorizationCodeTokenUrl = "https://auth.example.com/code-token",
            AuthorizationCodeRefreshUrl = "https://auth.example.com/refresh",
        };

        var result = OAuthOptionsExtractor.Extract(config, "TestApi");

        Assert.NotNull(result);
        Assert.Contains("public string TokenUrl { get; set; } = \"https://auth.example.com/code-token\";", result, StringComparison.Ordinal);
        Assert.Contains("/// Default from OpenAPI spec: https://auth.example.com/refresh", result, StringComparison.Ordinal);
        Assert.Contains("public string? RefreshUrl { get; set; } = \"https://auth.example.com/refresh\";", result, StringComparison.Ordinal);

        // No scopes on the config: the Scopes default is empty and gets no "Default from OpenAPI spec" line.
        Assert.Contains("public string Scopes { get; set; } = \"\";", result, StringComparison.Ordinal);
    }
}