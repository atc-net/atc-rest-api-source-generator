namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Configuration extracted from OAuth2 security schemes.
/// </summary>
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Used for code generation")]
public class OAuthConfig
{
    /// <summary>
    /// Gets or sets the security scheme name (e.g., "oauth2", "petstore_auth").
    /// </summary>
    public string SchemeName { get; set; } = "oauth2";

    /// <summary>
    /// Gets or sets a value indicating whether Client Credentials flow is configured.
    /// </summary>
    public bool HasClientCredentials { get; set; }

    /// <summary>
    /// Gets or sets the token URL for Client Credentials flow.
    /// </summary>
    public string? ClientCredentialsTokenUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Authorization Code flow is configured.
    /// </summary>
    public bool HasAuthorizationCode { get; set; }

    /// <summary>
    /// Gets or sets the token URL for Authorization Code flow.
    /// </summary>
    public string? AuthorizationCodeTokenUrl { get; set; }

    /// <summary>
    /// Gets or sets the refresh URL for Authorization Code flow.
    /// </summary>
    public string? AuthorizationCodeRefreshUrl { get; set; }

    /// <summary>
    /// Gets the default scopes from the configured flows.
    /// Key is scope name, value is description.
    /// </summary>
    public Dictionary<string, string> DefaultScopes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets all available scopes across all OAuth2 flows.
    /// Key is scope name, value is description.
    /// </summary>
    public Dictionary<string, string> AllAvailableScopes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the effective token URL (prefers Client Credentials, falls back to Authorization Code).
    /// </summary>
    public string? EffectiveTokenUrl
        => ClientCredentialsTokenUrl ?? AuthorizationCodeTokenUrl;

    /// <summary>
    /// Gets a value indicating whether refresh tokens are supported.
    /// </summary>
    public bool SupportsRefreshTokens
        => !string.IsNullOrEmpty(AuthorizationCodeRefreshUrl);

    /// <summary>
    /// Gets the default scope string (space-separated) for use in token requests.
    /// </summary>
    public string DefaultScopeString
        => DefaultScopes.Count > 0
            ? string.Join(" ", DefaultScopes.Keys)
            : string.Empty;
}