namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Model representing authorization configuration for an API operation.
/// </summary>
/// <param name="Roles">The list of roles required for authorization.</param>
/// <param name="AuthenticationSchemes">The list of authentication schemes allowed.</param>
/// <param name="UseAllowAnonymous">Whether to allow anonymous access (overrides authentication requirement).</param>
public record ApiAuthorizeModel(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> AuthenticationSchemes,
    bool UseAllowAnonymous);