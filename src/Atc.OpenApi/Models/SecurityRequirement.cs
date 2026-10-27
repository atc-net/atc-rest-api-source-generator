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
    /// Gets or sets the ASP.NET Core authentication scheme the security scheme stands for:
    /// its <c>x-authentication-scheme</c>, <c>"Bearer"</c> for <c>type: http, scheme: bearer</c>,
    /// else <see cref="SchemeName"/>.
    /// </summary>
    public string AuthenticationScheme { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the required scopes (for OAuth2).
    /// </summary>
    public IReadOnlyList<string> Scopes { get; set; } = [];
}