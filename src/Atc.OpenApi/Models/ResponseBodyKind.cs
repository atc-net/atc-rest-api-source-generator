namespace Atc.OpenApi.Models;

/// <summary>
/// How a request or response body of a declared media type is read and written. Decided by
/// <see cref="Helpers.ResponseBodyClassifier"/>, so the server and every client agree.
/// </summary>
public enum ResponseBodyKind
{
    /// <summary>
    /// No body (no content declared).
    /// </summary>
    None,

    /// <summary>
    /// JSON: <c>application/json</c> or any <c>+json</c> media type.
    /// </summary>
    Json,

    /// <summary>
    /// An XML model: <c>application/xml</c>, <c>text/xml</c> or any <c>+xml</c> media type with an object, array or
    /// model-reference schema.
    /// </summary>
    Xml,

    /// <summary>
    /// Text: <c>text/*</c>, or XML with a string schema (or none).
    /// </summary>
    Text,

    /// <summary>
    /// A stream of items: a media type that declares an <c>itemSchema</c> (OpenAPI 3.2), or a sequential media type
    /// (<see cref="StreamingMediaType.IsSequential"/>: <c>text/event-stream</c>, <c>application/jsonl</c>,
    /// <c>application/x-ndjson</c>, <c>application/x-jsonlines</c>, <c>application/json-seq</c>, <c>multipart/mixed</c>).
    /// </summary>
    Stream,

    /// <summary>
    /// A binary file: <c>application/octet-stream</c>, PDF, Office documents, images, … - and any other media type
    /// that is neither JSON, XML nor text.
    /// </summary>
    Binary,
}