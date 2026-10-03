namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class OAuthHandlerExtractorTests
{
    [Fact]
    public void Extract_ProjectName_GeneratesDelegatingHandlerThatRefreshesOnUnauthorized()
    {
        var result = OAuthHandlerExtractor.Extract("TestApi");

        Assert.Contains("namespace TestApi.Generated.OAuth;", result, StringComparison.Ordinal);
        Assert.Contains("public sealed class OAuthAuthenticationHandler : DelegatingHandler", result, StringComparison.Ordinal);
        Assert.Contains("public OAuthAuthenticationHandler(IOAuthTokenProvider tokenProvider)", result, StringComparison.Ordinal);

        // Bearer token on the way out, one refresh-and-retry on a 401.
        Assert.Contains("var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);", result, StringComparison.Ordinal);
        Assert.Contains("request.Headers.Authorization = new AuthenticationHeaderValue(\"Bearer\", token);", result, StringComparison.Ordinal);
        Assert.Contains("if (response.StatusCode == HttpStatusCode.Unauthorized)", result, StringComparison.Ordinal);
        Assert.Contains("token = await _tokenProvider.RefreshTokenAsync(cancellationToken);", result, StringComparison.Ordinal);
        Assert.Contains("using var retryRequest = await CloneRequestAsync(request);", result, StringComparison.Ordinal);
        Assert.Contains("private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)", result, StringComparison.Ordinal);

        // Usings are derived from the emitted content.
        Assert.Contains("using System.Net;", result, StringComparison.Ordinal);
        Assert.Contains("using System.Net.Http;", result, StringComparison.Ordinal);
        Assert.Contains("using System.Net.Http.Headers;", result, StringComparison.Ordinal);
        Assert.Contains("using System.Threading;", result, StringComparison.Ordinal);
        Assert.Contains("using System.Threading.Tasks;", result, StringComparison.Ordinal);
    }
}