namespace Atc.OpenApi.Extensions;

/// <summary>
/// What a media type is, and the body of a content map under one - one rule for every generator: the base type without
/// its parameters (<c>application/json; charset=utf-8</c> is <c>application/json</c>), JSON (<c>application/json</c> or
/// any <c>+json</c> type, e.g. <c>application/hal+json</c>) and XML (<c>application/xml</c>, <c>text/xml</c> or any
/// <c>+xml</c> type).
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
[SuppressMessage("", "CA1708:Names should differ by more than case", Justification = "OK - CLang14 - multiple extension(...) blocks")]
public static class MediaTypeExtensions
{
    /// <param name="mediaType">A media type, as declared.</param>
    extension(string? mediaType)
    {
        /// <summary>
        /// Gets the media type without its parameters, trimmed.
        /// </summary>
        public string BaseType
            => (mediaType ?? string.Empty).Split(';')[0].Trim();

        /// <summary>
        /// Gets a value indicating whether the media type is JSON: <c>application/json</c> or a <c>+json</c> type.
        /// </summary>
        public bool IsJson
            => mediaType.BaseType is var type &&
               (type.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
                type.EndsWith("+json", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets a value indicating whether the media type is XML: <c>application/xml</c>, <c>text/xml</c> or a
        /// <c>+xml</c> type.
        /// </summary>
        public bool IsXml
            => mediaType.BaseType is var type &&
               (type.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("text/xml", StringComparison.OrdinalIgnoreCase) ||
                type.EndsWith("+xml", StringComparison.OrdinalIgnoreCase));
    }

    /// <param name="content">The content of a request body or a response.</param>
    extension(IDictionary<string, IOpenApiMediaType>? content)
    {
        /// <summary>
        /// Gets the JSON body of the content: under <c>application/json</c>, else under the first other JSON media type
        /// (<c>application/hal+json</c>, <c>application/vnd.x+json</c>); <see langword="null"/> when there is none.
        /// </summary>
        public IOpenApiMediaType? JsonBody
            => content is null
                ? null
                : content.TryGetValue("application/json", out var json)
                    ? json
                    : content.FirstOrDefault(entry => entry.Key.IsJson).Value;
    }
}