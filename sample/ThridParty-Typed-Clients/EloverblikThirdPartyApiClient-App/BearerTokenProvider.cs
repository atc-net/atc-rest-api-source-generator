namespace EloverblikThirdPartyApiClientApp;

/// <summary>
/// Holds the bearer token currently used by the typed client's HttpClient.
/// </summary>
/// <remarks>
/// Eloverblik uses a two-step authentication flow: a long-lived refresh token is
/// exchanged for a short-lived access token. Because <c>IHttpClientFactory</c>
/// pools and reuses handlers, the token cannot be baked into
/// <c>DefaultRequestHeaders</c> at registration time. Instead this singleton acts
/// as a mutable holder that <see cref="BearerTokenHandler"/> reads on every
/// outgoing request, which keeps the swap from refresh token to access token
/// visible to the generated client without rebuilding it.
/// </remarks>
internal sealed class BearerTokenProvider
{
    public string? Token { get; set; }
}