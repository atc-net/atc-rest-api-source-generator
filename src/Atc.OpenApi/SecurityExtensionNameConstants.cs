namespace Atc.OpenApi;

/// <summary>
/// Constant names for custom OpenAPI security extension tags.
/// </summary>
/// <remarks>
/// These extensions are specific to the ATC generator and allow configuration of
/// ASP.NET Core authorization at the root, path, and operation levels:
/// - x-authentication-required: Enables/disables authentication requirement
/// - x-authentication-schemes: Specifies allowed authentication schemes
/// - x-authorize-roles: Specifies required authorization roles
/// </remarks>
public static class SecurityExtensionNameConstants
{
    /// <summary>
    /// Extension tag for specifying whether authentication is required.
    /// Type: boolean
    /// Example: x-authentication-required: true
    /// </summary>
    public const string AuthenticationRequired = "x-authentication-required";

    /// <summary>
    /// Extension tag for specifying allowed authentication schemes.
    /// Type: string[]
    /// Example: x-authentication-schemes: ["Bearer", "ApiKey"]
    /// </summary>
    public const string AuthenticationSchemes = "x-authentication-schemes";

    /// <summary>
    /// Extension tag for specifying required authorization roles.
    /// Type: string[]
    /// Example: x-authorize-roles: ["admin", "manager"]
    /// </summary>
    public const string AuthorizeRoles = "x-authorize-roles";
}
