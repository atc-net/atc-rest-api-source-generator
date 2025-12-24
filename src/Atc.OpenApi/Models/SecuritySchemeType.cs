namespace Atc.OpenApi.Models;

/// <summary>
/// Type of security scheme as defined in OpenAPI 3.0.
/// </summary>
public enum SecuritySchemeType
{
    /// <summary>
    /// HTTP authentication (Bearer, Basic).
    /// </summary>
    Http,

    /// <summary>
    /// API key authentication (header, query, cookie).
    /// </summary>
    ApiKey,

    /// <summary>
    /// OAuth 2.0 authentication.
    /// </summary>
    OAuth2,

    /// <summary>
    /// OpenID Connect Discovery.
    /// </summary>
    OpenIdConnect,
}