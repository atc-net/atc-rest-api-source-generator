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
    {
        if (string.IsNullOrEmpty(mediaType))
        {
            return StreamingFraming.JsonArray;
        }

        var baseType = mediaType;
        var semicolon = baseType.IndexOf(';');
        if (semicolon >= 0)
        {
            baseType = baseType.Substring(0, semicolon);
        }

        baseType = baseType.Trim();

        if (baseType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return StreamingFraming.ServerSentEvents;
        }

        if (baseType.Equals("application/jsonl", StringComparison.OrdinalIgnoreCase) ||
            baseType.Equals("application/x-ndjson", StringComparison.OrdinalIgnoreCase) ||
            baseType.Equals("application/x-jsonlines", StringComparison.OrdinalIgnoreCase))
        {
            return StreamingFraming.JsonLines;
        }

        if (baseType.Equals("application/json-seq", StringComparison.OrdinalIgnoreCase))
        {
            return StreamingFraming.JsonSequence;
        }

        if (baseType.Equals("multipart/mixed", StringComparison.OrdinalIgnoreCase))
        {
            return StreamingFraming.MultipartMixed;
        }

        return StreamingFraming.JsonArray;
    }
}
