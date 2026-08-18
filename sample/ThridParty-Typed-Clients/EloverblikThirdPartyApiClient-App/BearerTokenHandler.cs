namespace EloverblikThirdPartyApiClientApp;

/// <summary>
/// Attaches the current bearer token from <see cref="BearerTokenProvider"/> to every
/// request made through the typed client's HttpClient.
/// </summary>
/// <remarks>
/// Registering cross-cutting transport concerns as a <see cref="DelegatingHandler"/>
/// keeps the generated client free of authentication code - it only ever deals
/// with the OpenAPI contract.
/// </remarks>
internal sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly BearerTokenProvider tokenProvider;

    public BearerTokenHandler(BearerTokenProvider tokenProvider)
        => this.tokenProvider = tokenProvider;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = tokenProvider.Token;

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}