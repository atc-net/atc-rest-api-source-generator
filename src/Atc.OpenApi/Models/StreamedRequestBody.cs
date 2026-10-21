namespace Atc.OpenApi.Models;

/// <summary>
/// A request body sent as a stream of records - JSON Lines (<c>application/jsonl</c>, <c>application/x-ndjson</c>,
/// <c>application/x-jsonlines</c>) or JSON Text Sequences (<c>application/json-seq</c>, any <c>+json-seq</c>) - read
/// and written one record at a time by the server and every client.
/// </summary>
/// <param name="MediaTypes">The sequential media types the body declares, in the specification's order (the first is
/// the one clients send).</param>
/// <param name="ItemSchema">The schema of one record: an <c>itemSchema</c>, the items of an array <c>schema</c>, or the
/// <c>schema</c> itself.</param>
/// <param name="MinItems">The fewest records the body may have (the array schema's <c>minItems</c>), or
/// <see langword="null"/>.</param>
/// <param name="MaxItems">The most records the body may have (the array schema's <c>maxItems</c>), or
/// <see langword="null"/>.</param>
/// <param name="JsonMediaType">The JSON media type the body is offered as too (<c>application/json</c>, a <c>+json</c>
/// type) - the records as a JSON array, or one record - or <see langword="null"/>.</param>
/// <param name="JsonIsArray">Whether that JSON body is an array of the records (else it is one record).</param>
/// <param name="IsRequired">Whether the request body is required.</param>
public sealed record StreamedRequestBody(
    IReadOnlyList<string> MediaTypes,
    IOpenApiSchema ItemSchema,
    int? MinItems,
    int? MaxItems,
    string? JsonMediaType,
    bool JsonIsArray,
    bool IsRequired)
{
    /// <summary>
    /// The media type clients send: the first declared.
    /// </summary>
    public string PrimaryMediaType => MediaTypes[0];

    /// <summary>
    /// The framing clients send.
    /// </summary>
    public StreamingFraming PrimaryFraming
        => StreamingMediaType.Classify(PrimaryMediaType);

    /// <summary>
    /// Every media type the server takes for the body: the declared ones, every JSON Lines name when one is declared
    /// (<c>application/jsonl</c>, <c>application/x-ndjson</c>, <c>application/x-jsonlines</c> are the same body), and
    /// the JSON media type the body is offered as too.
    /// </summary>
    public IReadOnlyList<string> AcceptedMediaTypes
    {
        get
        {
            var accepted = new List<string>(MediaTypes);
            if (MediaTypes.Any(m => StreamingMediaType.Classify(m) == StreamingFraming.JsonLines))
            {
                foreach (var alias in JsonLinesMediaTypes)
                {
                    if (!accepted.Exists(m => string.Equals(m.Split(';')[0].Trim(), alias, StringComparison.OrdinalIgnoreCase)))
                    {
                        accepted.Add(alias);
                    }
                }
            }

            if (JsonMediaType is not null)
            {
                accepted.Add(JsonMediaType);
            }

            return accepted;
        }
    }

    /// <summary>
    /// The names JSON Lines goes by.
    /// </summary>
    public static IReadOnlyList<string> JsonLinesMediaTypes { get; } = ["application/jsonl", "application/x-ndjson", "application/x-jsonlines"];
}