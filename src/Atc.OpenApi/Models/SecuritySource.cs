namespace Atc.OpenApi.Models;

/// <summary>
/// Source of security configuration.
/// </summary>
public enum SecuritySource
{
    /// <summary>
    /// No security configured.
    /// </summary>
    None,

    /// <summary>
    /// Security from ATC extensions (x-authentication-*, x-authorize-roles).
    /// </summary>
    AtcExtensions,

    /// <summary>
    /// Security from standard OpenAPI security schemes and requirements.
    /// </summary>
    OpenApiSecuritySchemes,

    /// <summary>
    /// Security from both ATC extensions and standard OpenAPI.
    /// </summary>
    Both,
}