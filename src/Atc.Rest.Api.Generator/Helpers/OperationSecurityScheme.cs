namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// A credential an operation accepts, as a generated client sends it: a bearer token (http bearer, OAuth2, OpenID
/// Connect) or an API key in the header, query parameter or cookie its scheme declares.
/// </summary>
/// <param name="Name">The scheme's name in <c>components.securitySchemes</c> (<c>bearer</c> for a requirement the ATC
/// extensions alone make).</param>
/// <param name="IsBearer">Whether it is a bearer token; else an API key.</param>
/// <param name="In">Where an API key goes: <c>header</c>, <c>query</c> or <c>cookie</c>; <see langword="null"/> for a
/// bearer token.</param>
/// <param name="ParameterName">The header, query parameter or cookie an API key goes in; <see langword="null"/> for a
/// bearer token.</param>
public sealed record OperationSecurityScheme(
    string Name,
    bool IsBearer,
    string? In,
    string? ParameterName);