namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for MinimalApiPackageMode that handles strings, booleans, and various formats.
/// </summary>
/// <remarks>
/// Supported formats:
/// - "auto" → Auto
/// - true / "true" / "enabled" → Enabled
/// - false / "false" / "disabled" → Disabled
/// </remarks>
public class MinimalApiPackageModeConverter : JsonConverter<MinimalApiPackageMode>
{
    /// <inheritdoc />
    public override MinimalApiPackageMode Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True)
        {
            return MinimalApiPackageMode.Enabled;
        }

        if (reader.TokenType == JsonTokenType.False)
        {
            return MinimalApiPackageMode.Disabled;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return value?.ToLowerInvariant() switch
            {
                "auto" => MinimalApiPackageMode.Auto,
                "true" or "enabled" => MinimalApiPackageMode.Enabled,
                "false" or "disabled" => MinimalApiPackageMode.Disabled,
                _ => MinimalApiPackageMode.Auto, // Default fallback
            };
        }

        return MinimalApiPackageMode.Auto; // Default fallback for unexpected token types
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        MinimalApiPackageMode value,
        JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            MinimalApiPackageMode.Enabled => "enabled",
            MinimalApiPackageMode.Disabled => "disabled",
            _ => "auto",
        };
        writer.WriteStringValue(stringValue);
    }
}