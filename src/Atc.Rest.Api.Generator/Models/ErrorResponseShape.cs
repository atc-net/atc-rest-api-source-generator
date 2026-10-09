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
    /// Whether the body can hold the ProblemDetails a host writes for an error response without a body of its own: a
    /// ProblemDetails, or a declared model with a <c>title</c>, <c>status</c> or <c>detail</c> property (also through
    /// <c>allOf</c>). A handler's factory for any other declared model takes the model, so the response always carries it.
    /// </summary>
    public bool CanHoldProblemDetails
        => IsProblem ||
           (Kind is ErrorResponseKind.JsonObject or ErrorResponseKind.XmlObject && HasProblemMember(Schema, depth: 0));

    private static bool HasProblemMember(
        IOpenApiSchema? schema,
        int depth)
    {
        if (schema is null || depth > 16)
        {
            return false;
        }

        if (schema.Properties is { Count: > 0 } properties &&
            properties.Keys.Any(name => name.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                                        name.Equals("status", StringComparison.OrdinalIgnoreCase) ||
                                        name.Equals("detail", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return schema.AllOf is { Count: > 0 } parts && parts.Any(part => HasProblemMember(part, depth + 1));
    }

    /// <summary>
    /// The media type as the content of a C# string literal (quotes and backslashes of a parameter such as
    /// <c>profile="urn:x"</c> escaped).
    /// </summary>
    public string? MediaTypeLiteral
        => MediaType?.Replace("\\", "\\\\").Replace("\"", "\\\"");
}