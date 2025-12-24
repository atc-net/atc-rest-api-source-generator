namespace Atc.OpenApi.Models;

/// <summary>
/// Represents a security requirement from a 'security' array entry.
/// </summary>
public sealed class SecurityRequirement
{
    /// <summary>
    /// Gets or sets the security scheme name (references securitySchemes).
    /// </summary>
    public string SchemeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the required scopes (for OAuth2).
    /// </summary>
    public IReadOnlyList<string> Scopes { get; set; } = [];
}