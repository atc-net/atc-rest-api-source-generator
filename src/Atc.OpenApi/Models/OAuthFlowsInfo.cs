namespace Atc.OpenApi.Models;

/// <summary>
/// Information about OAuth2 flows.
/// </summary>
public sealed class OAuthFlowsInfo
{
    /// <summary>
    /// Gets or sets the implicit flow configuration.
    /// </summary>
    public OAuthFlowInfo? Implicit { get; set; }

    /// <summary>
    /// Gets or sets the password flow configuration.
    /// </summary>
    public OAuthFlowInfo? Password { get; set; }

    /// <summary>
    /// Gets or sets the client credentials flow configuration.
    /// </summary>
    public OAuthFlowInfo? ClientCredentials { get; set; }

    /// <summary>
    /// Gets or sets the authorization code flow configuration.
    /// </summary>
    public OAuthFlowInfo? AuthorizationCode { get; set; }
}