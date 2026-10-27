namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// An XML model body of a request or response.
/// </summary>
/// <param name="MediaType">The declared media type (<c>application/xml</c>, <c>text/xml</c>, <c>*+xml</c>).</param>
/// <param name="IsList">Whether the body is an array of the model.</param>
/// <param name="ListRootName">The root element of an array body (<c>xml.name</c> of the array), or <see langword="null"/> for the default.</param>
public sealed record XmlBody(
    string MediaType,
    bool IsList,
    string? ListRootName)
{
    /// <summary>
    /// The XML media types of a body that is offered as JSON too (negotiated by <c>Accept</c>), or
    /// <see langword="null"/> for an XML-only body.
    /// </summary>
    public IReadOnlyList<string>? AlternativeMediaTypes { get; init; }

    /// <summary>
    /// Whether the body is offered as JSON too, and the XML is the alternative negotiated by <c>Accept</c>.
    /// </summary>
    public bool IsAlternative
        => AlternativeMediaTypes is not null;

    /// <summary>
    /// The JSON media types of a model body that is offered as JSON only, which the generated server reads with its own
    /// reader too (a value that does not parse is a 400 naming the member); <see langword="null"/> for an XML body.
    /// </summary>
    public IReadOnlyList<string>? JsonOnlyMediaTypes { get; init; }

    /// <summary>
    /// Whether the body is a model offered as JSON only (no XML media type).
    /// </summary>
    public bool IsJsonOnly
        => JsonOnlyMediaTypes is not null;
}