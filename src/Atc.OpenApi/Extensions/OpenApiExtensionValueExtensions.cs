namespace Atc.OpenApi.Extensions;

/// <summary>
/// Reads the value of an <c>x-</c> extension (<c>x-retry-max-attempts</c>, <c>x-cache-tags</c>, <c>x-authorize-roles</c>,
/// ...) - one reader for every extension the generator understands. A number read from YAML arrives as a
/// <see cref="decimal"/>, from JSON as an <see cref="int"/>, <see cref="long"/> or <see cref="double"/>: each is
/// accepted.
/// </summary>
[SuppressMessage("", "CA1034:Do not nest type", Justification = "OK - CLang14 - extension")]
public static class OpenApiExtensionValueExtensions
{
    /// <param name="extensions">The extensions of a document, path, operation, schema or security scheme.</param>
    extension(IDictionary<string, IOpenApiExtension>? extensions)
    {
        /// <summary>
        /// The JSON value of an extension.
        /// </summary>
        /// <param name="key">The extension name.</param>
        /// <returns>The value, or <see langword="null"/> when the extension is absent.</returns>
        public JsonNode? NodeOf(string key)
            => extensions is not null &&
               extensions.TryGetValue(key, out var extension) &&
               extension is JsonNodeExtension { Node: { } node }
                ? node
                : null;

        /// <summary>
        /// The text of an extension.
        /// </summary>
        /// <param name="key">The extension name.</param>
        /// <returns>The text, or <see langword="null"/> when the extension is absent or no text.</returns>
        public string? ReadString(string key)
            => extensions.NodeOf(key) is JsonValue value && value.TryGetValue<string>(out var text)
                ? text
                : null;

        /// <summary>
        /// The boolean of an extension.
        /// </summary>
        /// <param name="key">The extension name.</param>
        /// <returns>The boolean, or <see langword="null"/> when the extension is absent or no boolean.</returns>
        public bool? ReadBool(string key)
            => extensions.NodeOf(key) is JsonValue value && value.TryGetValue<bool>(out var flag)
                ? flag
                : null;

        /// <summary>
        /// The whole number of an extension, clamped to the <see cref="int"/> range.
        /// </summary>
        /// <param name="key">The extension name.</param>
        /// <returns>The number, or <see langword="null"/> when the extension is absent or no number.</returns>
        public int? ReadInt(string key)
        {
            if (extensions.NodeOf(key) is not JsonValue value)
            {
                return null;
            }

            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (value.TryGetValue<long>(out var longValue))
            {
                return longValue > int.MaxValue ? int.MaxValue : longValue < int.MinValue ? int.MinValue : (int)longValue;
            }

            if (value.TryGetValue<decimal>(out var decimalValue))
            {
                return decimalValue > int.MaxValue ? int.MaxValue : decimalValue < int.MinValue ? int.MinValue : (int)decimalValue;
            }

            return null;
        }

        /// <summary>
        /// The number of an extension.
        /// </summary>
        /// <param name="key">The extension name.</param>
        /// <returns>The number, or <see langword="null"/> when the extension is absent or no number.</returns>
        public double? ReadDouble(string key)
        {
            if (extensions.NodeOf(key) is not JsonValue value)
            {
                return null;
            }

            if (value.TryGetValue<double>(out var doubleValue))
            {
                return doubleValue;
            }

            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (value.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }

            return value.TryGetValue<decimal>(out var decimalValue)
                ? (double)decimalValue
                : null;
        }

        /// <summary>
        /// The texts of an extension that is an array.
        /// </summary>
        /// <param name="key">The extension name.</param>
        /// <returns>The texts in order, or <see langword="null"/> when the extension is absent, no array or holds no text.</returns>
        public IReadOnlyList<string>? ReadStringArray(string key)
        {
            if (extensions.NodeOf(key) is not JsonArray array)
            {
                return null;
            }

            var result = new List<string>();
            foreach (var item in array)
            {
                if (item is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    result.Add(text);
                }
            }

            return result.Count > 0 ? result : null;
        }
    }
}