namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for <see cref="InlineParameterEnumMode"/> accepting "Enum" or "String"
/// (case-insensitive). Unknown values fall back to <see cref="InlineParameterEnumMode.Enum"/>.
/// </summary>
public class InlineParameterEnumModeConverter : JsonConverter<InlineParameterEnumMode>
{
    /// <inheritdoc />
    public override InlineParameterEnumMode Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "string" => InlineParameterEnumMode.String,
            _ => InlineParameterEnumMode.Enum,
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        InlineParameterEnumMode value,
        JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            InlineParameterEnumMode.String => "String",
            _ => "Enum",
        };
        writer.WriteStringValue(stringValue);
    }
}