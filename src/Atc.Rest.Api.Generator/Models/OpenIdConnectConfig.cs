namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Configuration extracted from OpenID Connect security schemes.
/// </summary>
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Used for code generation")]
public class OpenIdConnectConfig
{
    /// <summary>
    /// Gets or sets the security scheme name (e.g., "oidc", "openIdConnect").
    /// </summary>
    public string SchemeName { get; set; } = "openIdConnect";

    /// <summary>
    /// Gets or sets the OpenID Connect discovery URL.
    /// This is the well-known configuration endpoint (.well-known/openid-configuration).
    /// </summary>
    public string? OpenIdConnectUrl { get; set; }

    /// <summary>
    /// Gets or sets the description from the security scheme.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets the default scopes from the security requirements.
    /// Key is scope name, value is description.
    /// </summary>
    public Dictionary<string, string> DefaultScopes { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the default scope string (space-separated) for use in authentication.
    /// If no scopes are specified, defaults to standard OpenID Connect scopes.
    /// </summary>
    public string DefaultScopeString
        => DefaultScopes.Count > 0
            ? string.Join(" ", DefaultScopes.Keys)
            : "openid profile";

    /// <summary>
    /// Gets a value indicating whether the discovery URL is configured.
    /// </summary>
    public bool HasDiscoveryUrl
        => !string.IsNullOrEmpty(OpenIdConnectUrl);
}