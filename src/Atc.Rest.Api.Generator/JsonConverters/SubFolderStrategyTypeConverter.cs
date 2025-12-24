// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for SubFolderStrategyType that handles both PascalCase and kebab-case strings.
/// </summary>
public class SubFolderStrategyTypeConverter : JsonConverter<SubFolderStrategyType>
{
    /// <inheritdoc />
    public override SubFolderStrategyType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "none" => SubFolderStrategyType.None,
            "first-path-segment" or "firstpathsegment" => SubFolderStrategyType.FirstPathSegment,
            "openapi-tag" or "openapitag" => SubFolderStrategyType.OpenApiTag,
            _ => SubFolderStrategyType.None, // Default fallback
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        SubFolderStrategyType value,
        JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            SubFolderStrategyType.FirstPathSegment => "FirstPathSegment",
            SubFolderStrategyType.OpenApiTag => "OpenApiTag",
            _ => "None",
        };
        writer.WriteStringValue(stringValue);
    }
}