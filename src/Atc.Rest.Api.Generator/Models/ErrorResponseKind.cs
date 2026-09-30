namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// How an error response (4xx, 5xx or <c>default</c>) is written on the wire, as decided by what the
/// specification declares for it.
/// </summary>
public enum ErrorResponseKind
{
    /// <summary>
    /// An RFC 9457 ProblemDetails as <c>application/problem+json</c>: nothing is declared, or the declared
    /// schema is named <c>ProblemDetails</c>.
    /// </summary>
    Problem,

    /// <summary>
    /// An RFC 9457 validation ProblemDetails (<c>errors</c> dictionary): the declared schema is named
    /// <c>ValidationProblemDetails</c>.
    /// </summary>
    ValidationProblem,

    /// <summary>
    /// A JSON object (or array) of the declared schema, with the declared JSON media type.
    /// </summary>
    JsonObject,

    /// <summary>
    /// A JSON string (<c>type: string</c> under a JSON media type).
    /// </summary>
    JsonString,

    /// <summary>
    /// Raw text (<c>text/*</c> or <c>application/xml</c> with a string schema).
    /// </summary>
    Text,

    /// <summary>
    /// A declared model written as XML (<c>application/xml</c>, <c>text/xml</c>, <c>*+xml</c>) by the model's generated
    /// XML mapping.
    /// </summary>
    XmlObject,
}