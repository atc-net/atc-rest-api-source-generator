namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Helper class for JSON serialization operations for property name.
/// </summary>
public static class JsonPropertyNameHelper
{
    /// <summary>
    /// Creates an AttributeParameters for JsonPropertyName if needed, otherwise returns null.
    /// </summary>
    public static AttributeParameters? CreateJsonPropertyNameAttribute(
        string jsonKey,
        string csharpPropertyName)
        => RequiresJsonPropertyName(jsonKey, csharpPropertyName)
            ? new AttributeParameters("JsonPropertyName", $"\"{jsonKey}\"")
            : null;

    /// <summary>
    /// Determines whether an OpenAPI property key requires an explicit JsonPropertyName attribute.
    /// Returns true if the key contains special characters (underscores, dots, hyphens),
    /// differs from standard camelCase of the C# property name, or has custom acronym casing.
    /// </summary>
    public static bool RequiresJsonPropertyName(
        string jsonKey,
        string csharpPropertyName)
    {
        if (string.IsNullOrEmpty(jsonKey) ||
            string.IsNullOrEmpty(csharpPropertyName))
        {
            return false;
        }

        // Exact match with C# name (e.g. PascalCase in JSON matching PascalCase C# name)
        if (string.Equals(jsonKey, csharpPropertyName, StringComparison.Ordinal))
        {
            return false;
        }

        // Standard camelCase match (e.g. "errorText" for "ErrorText", "id" for "Id")
        var camelCased = char.ToLowerInvariant(csharpPropertyName[0]) + csharpPropertyName.Substring(1);

        // All other cases require explicit mapping:
        // - Underscores: "MyEnergyData_MarketDocument", "error_code", "created_date_time"
        // - Dots: "sender_MarketParticipant.name", "period.timeInterval"
        // - Hyphens: "x-correlation-id", "api-key"
        // - Acronym mismatch: "mRID" vs "MRid" (camelCase is "mRid")
        // - Enclosing type collision rename: "Status" in "Status" model becomes "StatusValue"
        return !string.Equals(jsonKey, camelCased, StringComparison.Ordinal);
    }
}