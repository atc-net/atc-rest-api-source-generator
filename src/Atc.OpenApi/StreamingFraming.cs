namespace Atc.OpenApi;

/// <summary>
/// Wire framing for a streamed (sequential) response, derived from the declared
/// response media type. Drives how each generator layer reads/writes the stream.
/// </summary>
public enum StreamingFraming
{
    /// <summary>JSON array (<c>[{…},{…}]</c>). Legacy default; also the
    /// <c>application/json</c> + <c>x-return-async-enumerable</c> path.</summary>
    JsonArray,

    /// <summary>Server-Sent Events (<c>text/event-stream</c>): <c>data: &lt;json&gt;\n\n</c>.</summary>
    ServerSentEvents,

    /// <summary>JSON Lines / NDJSON (<c>application/jsonl</c>): <c>&lt;json&gt;\n</c>.</summary>
    JsonLines,

    /// <summary>JSON Text Sequence, RFC 7464 (<c>application/json-seq</c>): <c>\x1e&lt;json&gt;\n</c>.</summary>
    JsonSequence,

    /// <summary>Multipart mixed (<c>multipart/mixed</c>): boundary-delimited JSON parts.</summary>
    MultipartMixed,
}

/// <summary>Maps response media types to <see cref="StreamingFraming"/>.</summary>
public static class StreamingMediaType
{
    /// <summary>
    /// Classifies a declared response media type. Media-type parameters
    /// (e.g. <c>; charset=utf-8</c>) are ignored. Anything not recognized as a
    /// sequential framing maps to <see cref="StreamingFraming.JsonArray"/>.
    /// </summary>
    public static StreamingFraming Classify(string mediaType)
        => TryClassify(mediaType, out var framing) ? framing : StreamingFraming.JsonArray;

    /// <summary>
    /// Whether a media type is a sequential framing - a stream of items, not one document: Server-Sent Events,
    /// JSON Lines (<c>application/jsonl</c>, <c>application/x-ndjson</c>, <c>application/x-jsonlines</c>), JSON Text
    /// Sequences (<c>application/json-seq</c> and any <c>+json-seq</c> type, RFC 8091) or <c>multipart/mixed</c>.
    /// The one list every layer uses: <see cref="Helpers.ResponseBodyClassifier"/> calls a body of such a type a stream.
    /// </summary>
    public static bool IsSequential(string? mediaType)
        => TryClassify(mediaType, out _);

    /// <summary>
    /// The element of a sequential stream declared the pre-3.2 way: a <c>schema</c> under a streaming media
    /// type (<c>text/event-stream</c>, <c>application/jsonl</c>, ...) on an operation marked
    /// <c>x-return-async-enumerable</c>. The schema describes one element, or the array of them.
    /// <see langword="null"/> for a media type that is not a sequential framing.
    /// </summary>
    public static IOpenApiSchema? ItemSchemaOf(
        string mediaType,
        IOpenApiSchema? schema)
    {
        if (schema is null || Classify(mediaType) == StreamingFraming.JsonArray)
        {
            return null;
        }

        return schema.Type?.HasFlag(JsonSchemaType.Array) == true && schema.Items is not null
            ? schema.Items
            : schema;
    }

    private static bool TryClassify(
        string? mediaType,
        out StreamingFraming framing)
    {
        framing = StreamingFraming.JsonArray;
        if (string.IsNullOrEmpty(mediaType))
        {
            return false;
        }

        var baseType = mediaType!;
        var semicolon = baseType.IndexOf(';');
        if (semicolon >= 0)
        {
            baseType = baseType.Substring(0, semicolon);
        }

        baseType = baseType.Trim();

        if (baseType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            framing = StreamingFraming.ServerSentEvents;
            return true;
        }

        if (baseType.Equals("application/jsonl", StringComparison.OrdinalIgnoreCase) ||
            baseType.Equals("application/x-ndjson", StringComparison.OrdinalIgnoreCase) ||
            baseType.Equals("application/x-jsonlines", StringComparison.OrdinalIgnoreCase))
        {
            framing = StreamingFraming.JsonLines;
            return true;
        }

        if (baseType.Equals("application/json-seq", StringComparison.OrdinalIgnoreCase) ||
            baseType.EndsWith("+json-seq", StringComparison.OrdinalIgnoreCase))
        {
            framing = StreamingFraming.JsonSequence;
            return true;
        }

        if (baseType.Equals("multipart/mixed", StringComparison.OrdinalIgnoreCase))
        {
            framing = StreamingFraming.MultipartMixed;
            return true;
        }

        return false;
    }
}