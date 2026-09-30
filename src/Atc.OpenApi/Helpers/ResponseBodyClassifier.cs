namespace Atc.OpenApi.Helpers;

/// <summary>
/// The one rule that decides how a body of a declared media type is read and written - JSON, an XML model, text, or
/// a binary file. Shared by the server, the C# clients and the TypeScript client, so a declared body is never read
/// one way and written another, and never dropped: a media type that is neither JSON, XML nor text is a file.
/// </summary>
public static class ResponseBodyClassifier
{
    /// <summary>
    /// Classifies one declared media type.
    /// </summary>
    /// <param name="mediaType">The media type (parameters such as <c>charset</c> are ignored).</param>
    /// <param name="schema">The declared schema, or <see langword="null"/>.</param>
    /// <returns>The body kind; never <see cref="ResponseBodyKind.None"/> for a media type.</returns>
    public static ResponseBodyKind Classify(
        string mediaType,
        IOpenApiSchema? schema)
    {
        var type = (mediaType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();

        if (type is "text/event-stream" or "application/jsonl" or "application/x-ndjson" or "application/json-seq")
        {
            return ResponseBodyKind.Stream;
        }

        if (type == "application/json" || type.EndsWith("+json", StringComparison.Ordinal))
        {
            return ResponseBodyKind.Json;
        }

        if (type is "application/xml" or "text/xml" || type.EndsWith("+xml", StringComparison.Ordinal))
        {
            return IsModelSchema(schema) ? ResponseBodyKind.Xml : ResponseBodyKind.Text;
        }

        if (type.StartsWith("text/", StringComparison.Ordinal))
        {
            return IsBinarySchema(schema) ? ResponseBodyKind.Binary : ResponseBodyKind.Text;
        }

        return ResponseBodyKind.Binary;
    }

    /// <summary>
    /// Classifies every declared media type of a request or response body, in the specification's order.
    /// </summary>
    /// <param name="content">The <c>content</c> map, or <see langword="null"/>.</param>
    /// <returns>Each media type with its schema and kind; empty when nothing is declared.</returns>
    public static IReadOnlyList<ResponseBodyMedia> ClassifyContent(
        IDictionary<string, IOpenApiMediaType>? content)
        => content is null
            ? []
            : content
                .Select(kv => new ResponseBodyMedia(
                    kv.Key,
                    kv.Value?.ItemSchema is not null ? ResponseBodyKind.Stream : Classify(kv.Key, kv.Value?.Schema),
                    kv.Value?.Schema ?? kv.Value?.ItemSchema))
                .ToList();

    /// <summary>
    /// The primary body of a request or response: its first declared media type. A response that declares several
    /// (JSON and XML, JSON and a PDF) is read and written as its first one unless the caller negotiates another.
    /// </summary>
    /// <param name="content">The <c>content</c> map, or <see langword="null"/>.</param>
    /// <returns>The first media type with its kind, or <see langword="null"/> when nothing is declared.</returns>
    public static ResponseBodyMedia? PrimaryBody(
        IDictionary<string, IOpenApiMediaType>? content)
    {
        var bodies = ClassifyContent(content);
        return bodies.Count > 0 ? bodies[0] : null;
    }

    /// <summary>
    /// Whether a schema describes a model (an object, an array, or a reference to one) rather than a string.
    /// </summary>
    private static bool IsModelSchema(IOpenApiSchema? schema)
    {
        if (schema is null)
        {
            return false;
        }

        var target = schema is OpenApiSchemaReference { Target: { } referenced } ? referenced : schema;
        if (target.Type is { } type)
        {
            return type.HasFlag(JsonSchemaType.Object) || type.HasFlag(JsonSchemaType.Array);
        }

        // No type: a model when it has properties or composes other schemas.
        return target.Properties is { Count: > 0 } ||
               target.AllOf is { Count: > 0 } ||
               target.OneOf is { Count: > 0 } ||
               target.AnyOf is { Count: > 0 };
    }

    /// <summary>
    /// Whether a schema marks the body as binary: <c>format: binary</c> (OpenAPI 3.0).
    /// </summary>
    private static bool IsBinarySchema(IOpenApiSchema? schema)
        => string.Equals(schema?.Format, "binary", StringComparison.OrdinalIgnoreCase);
}