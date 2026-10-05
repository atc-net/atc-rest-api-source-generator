namespace Atc.OpenApi.Helpers;

/// <summary>
/// Reads a keyword the OpenAPI reader leaves as raw JSON (such as an entry of <c>prefixItems</c>): the specification
/// may hold any kind of value there, so a value of another kind is absent, not an exception.
/// </summary>
internal static class JsonKeywordReader
{
    /// <summary>
    /// The keyword's value when it is a string; otherwise <see langword="null"/>.
    /// </summary>
    /// <param name="node">The keyword's value, or <see langword="null"/> when the keyword is absent.</param>
    /// <returns>The string, or <see langword="null"/>.</returns>
    public static string? StringOrNull(JsonNode? node)
        => node is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : null;
}