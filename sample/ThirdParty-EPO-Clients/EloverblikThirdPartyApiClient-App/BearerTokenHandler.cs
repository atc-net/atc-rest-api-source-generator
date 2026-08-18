namespace EloverblikThirdPartyApiClientApp;

/// <summary>
/// Attaches the current bearer token from <see cref="BearerTokenProvider"/> to every
/// request made through the named HttpClient.
/// </summary>
/// <remarks>
/// Registering cross-cutting transport concerns as a <see cref="DelegatingHandler"/>
/// keeps the generated endpoints free of authentication code - they only ever deal
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