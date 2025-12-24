namespace Atc.OpenApi.Models;

/// <summary>
/// Represents information about an OpenAPI security scheme from components/securitySchemes.
/// </summary>
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "URLs stored as strings for simplicity.")]
public sealed class SecuritySchemeInfo
{
    /// <summary>
    /// Gets or sets the name of the security scheme (the key in securitySchemes).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of security scheme.
    /// </summary>
    public SecuritySchemeType Type { get; set; }

    /// <summary>
    /// Gets or sets the HTTP scheme (bearer, basic) for 'http' type.
    /// </summary>
    public string? Scheme { get; set; }

    /// <summary>
    /// Gets or sets the bearer format hint (JWT) for 'http' type with 'bearer' scheme.
    /// </summary>
    public string? BearerFormat { get; set; }

    /// <summary>
    /// Gets or sets the location of the API key (header, query, cookie) for 'apiKey' type.
    /// </summary>
    public ApiKeyLocation? In { get; set; }

    /// <summary>
    /// Gets or sets the parameter name for API key (e.g., "X-API-Key") for 'apiKey' type.
    /// </summary>
    public string? ParameterName { get; set; }

    /// <summary>
    /// Gets or sets the OAuth2 flows for 'oauth2' type.
    /// </summary>
    public OAuthFlowsInfo? Flows { get; set; }

    /// <summary>
    /// Gets or sets the OpenID Connect URL for 'openIdConnect' type.
    /// </summary>
    public string? OpenIdConnectUrl { get; set; }

    /// <summary>
    /// Gets or sets the description of the security scheme.
    /// </summary>
    public string? Description { get; set; }
}