namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OAuthTokenProviderExtractorTests
{
    [Fact]
    public void ExtractTokenResponse_ProjectName_GeneratesTokenEndpointModel()
    {
        var result = OAuthTokenProviderExtractor.ExtractTokenResponse("TestApi");

        Assert.Contains("namespace TestApi.Generated.OAuth;", result, StringComparison.Ordinal);
        Assert.Contains("using System.Text.Json.Serialization;", result, StringComparison.Ordinal);
        Assert.Contains("public sealed class OAuthTokenResponse", result, StringComparison.Ordinal);
        Assert.Contains("[JsonPropertyName(\"access_token\")]", result, StringComparison.Ordinal);
        Assert.Contains("public string AccessToken { get; set; } = string.Empty;", result, StringComparison.Ordinal);
        Assert.Contains("public string TokenType { get; set; } = \"Bearer\";", result, StringComparison.Ordinal);
        Assert.Contains("[JsonPropertyName(\"expires_in\")]", result, StringComparison.Ordinal);
        Assert.Contains("public int ExpiresIn { get; set; }", result, StringComparison.Ordinal);
        Assert.Contains("public string? RefreshToken { get; set; }", result, StringComparison.Ordinal);
        Assert.Contains("public string? Scope { get; set; }", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractInterface_ProjectName_GeneratesTokenProviderInterface()
    {
        var result = OAuthTokenProviderExtractor.ExtractInterface("TestApi");

        Assert.Contains("namespace TestApi.Generated.OAuth;", result, StringComparison.Ordinal);
        Assert.Contains("public interface IOAuthTokenProvider", result, StringComparison.Ordinal);
        Assert.Contains("Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);", result, StringComparison.Ordinal);
        Assert.Contains("Task<string> RefreshTokenAsync(CancellationToken cancellationToken = default);", result, StringComparison.Ordinal);
        Assert.Contains("using System.Threading;", result, StringComparison.Ordinal);
        Assert.Contains("using System.Threading.Tasks;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractImplementation_NullConfig_ReturnsNull()
    {
        Assert.Null(OAuthTokenProviderExtractor.ExtractImplementation(null, "TestApi"));
    }

    [Fact]
    public void ExtractImplementation_ClientCredentialsOnly_RequestsTokenWithClientCredentialsOnly()
    {
        var config = new OAuthConfig
        {
            HasClientCredentials = true,
            ClientCredentialsTokenUrl = "https://auth.example.com/token",
        };

        var result = OAuthTokenProviderExtractor.ExtractImplementation(config, "TestApi");

        Assert.NotNull(result);
        Assert.Contains("namespace TestApi.Generated.OAuth;", result, StringComparison.Ordinal);
        Assert.Contains("public sealed class OAuthTokenProvider : IOAuthTokenProvider, IDisposable", result, StringComparison.Ordinal);
        Assert.Contains("public OAuthTokenProvider(HttpClient httpClient, IOptions<OAuthClientOptions> options)", result, StringComparison.Ordinal);
        Assert.Contains("[\"grant_type\"] = \"client_credentials\",", result, StringComparison.Ordinal);
        Assert.Contains("tokenResponse = await RequestTokenWithClientCredentialsAsync(opts, cancellationToken).ConfigureAwait(false);", result, StringComparison.Ordinal);
        Assert.Contains("return await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);", result, StringComparison.Ordinal);

        // No refresh-token path without an Authorization Code refresh URL.
        Assert.DoesNotContain("RequestTokenWithRefreshTokenAsync", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\"refresh_token\"", result, StringComparison.Ordinal);

        Assert.Contains("using System.Net.Http.Json;", result, StringComparison.Ordinal);
        Assert.Contains("using Microsoft.Extensions.Options;", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractImplementation_WithRefreshUrl_TriesRefreshTokenBeforeClientCredentials()
    {
        var config = new OAuthConfig
        {
            HasAuthorizationCode = true,
            AuthorizationCodeTokenUrl = "https://auth.example.com/token",
            AuthorizationCodeRefreshUrl = "https://auth.example.com/refresh",
        };

        var result = OAuthTokenProviderExtractor.ExtractImplementation(config, "TestApi");

        Assert.NotNull(result);
        Assert.Contains("if (!string.IsNullOrEmpty(_refreshToken) && !string.IsNullOrEmpty(opts.RefreshUrl))", result, StringComparison.Ordinal);
        Assert.Contains("tokenResponse = await RequestTokenWithRefreshTokenAsync(opts, cancellationToken).ConfigureAwait(false);", result, StringComparison.Ordinal);
        Assert.Contains("private async Task<OAuthTokenResponse?> RequestTokenWithRefreshTokenAsync(", result, StringComparison.Ordinal);
        Assert.Contains("[\"grant_type\"] = \"refresh_token\",", result, StringComparison.Ordinal);

        // A failed refresh clears the stored refresh token and falls back to Client Credentials.
        Assert.Contains("_refreshToken = null;", result, StringComparison.Ordinal);
        Assert.Contains("return await RequestTokenWithClientCredentialsAsync(opts, cancellationToken).ConfigureAwait(false);", result, StringComparison.Ordinal);
    }
}