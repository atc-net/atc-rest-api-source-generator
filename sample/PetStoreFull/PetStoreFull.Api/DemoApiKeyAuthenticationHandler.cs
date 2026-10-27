namespace PetStoreFull.Api;

/// <summary>
/// The "api_key" scheme of the specification (an API key in the api_key header), for demo purposes.
/// </summary>
/// <remarks>
/// WARNING: Any value in the api_key header authenticates the caller. NEVER use this in production!
/// </remarks>
public sealed class DemoApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string HeaderName = "api_key";

    public DemoApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "demo-api-key-user"),
            new Claim(ClaimTypes.NameIdentifier, "demo-api-key-user-id"),
        };

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)),
            Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}