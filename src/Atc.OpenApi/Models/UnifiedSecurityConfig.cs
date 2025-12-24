namespace Atc.OpenApi.Models;

/// <summary>
/// Unified security configuration that merges both ATC extensions and standard OpenAPI security.
/// </summary>
public sealed class UnifiedSecurityConfig
{
    /// <summary>
    /// Gets or sets the source of security configuration.
    /// </summary>
    public SecuritySource Source { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether authentication is required.
    /// </summary>
    public bool AuthenticationRequired { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this endpoint is explicitly public (AllowAnonymous).
    /// </summary>
    public bool AllowAnonymous { get; set; }

    /// <summary>
    /// Gets or sets the required roles (from x-authorize-roles or policy).
    /// </summary>
    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the authentication schemes to use.
    /// </summary>
    public IReadOnlyList<string> Schemes { get; set; } = [];

    /// <summary>
    /// Gets or sets the named authorization policies to require.
    /// </summary>
    public IReadOnlyList<string> Policies { get; set; } = [];

    /// <summary>
    /// Gets or sets the OAuth2 scopes required.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; set; } = [];

    /// <summary>
    /// Gets or sets the security requirements (for standard OpenAPI security).
    /// </summary>
    public IReadOnlyList<SecurityRequirement> Requirements { get; set; } = [];
}
