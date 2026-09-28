namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// The resolved wire shape of one error response.
/// </summary>
/// <param name="Kind">How the body is written.</param>
/// <param name="MediaType">The declared media type, or <see langword="null"/> when nothing is declared.</param>
/// <param name="Schema">The declared schema, or <see langword="null"/> when nothing is declared.</param>
public sealed record ErrorResponseShape(
    ErrorResponseKind Kind,
    string? MediaType,
    IOpenApiSchema? Schema)
{
    /// <summary>
    /// Whether the body is an RFC 9457 ProblemDetails (plain or validation).
    /// </summary>
    public bool IsProblem
        => Kind is ErrorResponseKind.Problem or ErrorResponseKind.ValidationProblem;

    /// <summary>
    /// Whether the specification declares a body for this response at all.
    /// </summary>
    public bool IsDeclared
        => MediaType is not null;

    /// <summary>
    /// The media type as the content of a C# string literal (quotes and backslashes of a parameter such as
    /// <c>profile="urn:x"</c> escaped).
    /// </summary>
    public string? MediaTypeLiteral
        => MediaType?.Replace("\\", "\\\\").Replace("\"", "\\\"");
}