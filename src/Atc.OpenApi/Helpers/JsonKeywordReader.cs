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
    {
        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
        {
            return null;
        }

        // A value made in code may hold a Guid, a date or a char - a string in JSON, but not a .NET string
        return value.TryGetValue<string>(out var text)
            ? text
            : JsonSerializer.Deserialize<string>(value.ToJsonString());
    }
}