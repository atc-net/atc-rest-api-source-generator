namespace Atc.OpenApi.Models;

/// <summary>
/// Information about a single OAuth2 flow.
/// </summary>
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "URLs stored as strings for simplicity.")]
public sealed class OAuthFlowInfo
{
    /// <summary>
    /// Gets or sets the authorization URL (for implicit and authorizationCode flows).
    /// </summary>
    public string? AuthorizationUrl { get; set; }

    /// <summary>
    /// Gets or sets the token URL (for password, clientCredentials, and authorizationCode flows).
    /// </summary>
    public string? TokenUrl { get; set; }

    /// <summary>
    /// Gets or sets the refresh URL.
    /// </summary>
    public string? RefreshUrl { get; set; }

    /// <summary>
    /// Gets or sets the available scopes (name => description).
    /// </summary>
    public IReadOnlyDictionary<string, string> Scopes { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
}