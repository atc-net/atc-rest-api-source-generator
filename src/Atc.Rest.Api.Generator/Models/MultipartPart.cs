namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// How a field of a multipart form is sent: its encoding, and the media type its part declares.
/// </summary>
/// <param name="Encoding">The part's encoding.</param>
/// <param name="MediaType">The media type of an object part (<c>application/json</c>, the declared XML or URL-encoded one); null for a text part or none.</param>
public sealed record MultipartPart(
    MultipartPartEncoding Encoding,
    string? MediaType);