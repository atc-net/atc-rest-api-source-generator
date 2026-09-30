namespace Atc.OpenApi.Models;

/// <summary>
/// One declared media type of a body, with its schema and how it is read and written.
/// </summary>
/// <param name="MediaType">The media type as declared (e.g. <c>application/pdf</c>).</param>
/// <param name="Kind">How the body is read and written.</param>
/// <param name="Schema">The declared schema, or <see langword="null"/>.</param>
public sealed record ResponseBodyMedia(
    string MediaType,
    ResponseBodyKind Kind,
    IOpenApiSchema? Schema);