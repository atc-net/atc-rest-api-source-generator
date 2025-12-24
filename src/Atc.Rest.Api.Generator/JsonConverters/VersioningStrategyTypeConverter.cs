namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for VersioningStrategyType that handles both PascalCase and kebab-case strings.
/// </summary>
public class VersioningStrategyTypeConverter : JsonConverter<VersioningStrategyType>
{
    /// <inheritdoc />
    public override VersioningStrategyType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "none" => VersioningStrategyType.None,
            "query-string" or "querystring" => VersioningStrategyType.QueryString,
            "url-segment" or "urlsegment" => VersioningStrategyType.UrlSegment,
            "header" => VersioningStrategyType.Header,
            _ => VersioningStrategyType.None, // Default fallback
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        VersioningStrategyType value,
        JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            VersioningStrategyType.QueryString => "QueryString",
            VersioningStrategyType.UrlSegment => "UrlSegment",
            VersioningStrategyType.Header => "Header",
            _ => "None",
        };
        writer.WriteStringValue(stringValue);
    }
}