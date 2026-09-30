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
    string? ListRootName);