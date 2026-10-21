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

        // Every framing a stream comes in (StreamingFraming, one list): multipart/mixed too - boundary-delimited JSON
        // parts, not a file. Checked before JSON, so a "+json-seq" type is never read as one JSON document
        if (StreamingMediaType.IsSequential(type))
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
    /// (JSON and XML) is read and written as its first one unless the caller negotiates another; one that declares a
    /// model and a file (JSON and a PDF) is a model response whose files are alternatives (<see cref="FileAlternatives"/>),
    /// so its primary body is its first model (JSON or XML) in either order.
    /// </summary>
    /// <param name="content">The <c>content</c> map, or <see langword="null"/>.</param>
    /// <returns>The primary media type with its kind, or <see langword="null"/> when nothing is declared.</returns>
    public static ResponseBodyMedia? PrimaryBody(
        IDictionary<string, IOpenApiMediaType>? content)
    {
        var bodies = ClassifyContent(content);
        if (bodies.Count == 0)
        {
            return null;
        }

        return bodies[0].Kind == ResponseBodyKind.Binary && FileMediaTypesBesideAModel(bodies).Count > 0
            ? bodies.First(IsTypedModel)
            : bodies[0];
    }

    /// <summary>
    /// The file media types of a body that is a model <b>or</b> a file (<c>application/json</c> and
    /// <c>application/pdf</c>): the answer is the model or the file, told apart by its <c>Content-Type</c>.
    /// </summary>
    /// <param name="content">The <c>content</c> map, or <see langword="null"/>.</param>
    /// <returns>The file media types in the specification's order; empty unless a model (JSON or XML) is declared too.</returns>
    public static IReadOnlyList<string> FileAlternatives(
        IDictionary<string, IOpenApiMediaType>? content)
        => FileMediaTypesBesideAModel(ClassifyContent(content));

    private static IReadOnlyList<string> FileMediaTypesBesideAModel(
        IReadOnlyList<ResponseBodyMedia> bodies)
        => bodies.Any(IsTypedModel)
            ? bodies.Where(b => b.Kind == ResponseBodyKind.Binary).Select(b => b.MediaType).ToList()
            : [];

    /// <summary>
    /// Whether a body is a model the generator types: JSON with a schema, or XML of a component model (or a list of
    /// one). A file beside anything else (JSON without a schema, an inline XML object) stays a file download.
    /// </summary>
    private static bool IsTypedModel(ResponseBodyMedia body)
        => body.Kind switch
        {
            ResponseBodyKind.Json => body.Schema is not null,
            ResponseBodyKind.Xml => body.Schema is OpenApiSchemaReference || body.Schema?.Items is OpenApiSchemaReference,
            _ => false,
        };

    /// <summary>
    /// Whether a schema describes a model (an object, an array, or a reference to one) rather than a string.
    /// </summary>
    public static bool IsModelSchema(IOpenApiSchema? schema)
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