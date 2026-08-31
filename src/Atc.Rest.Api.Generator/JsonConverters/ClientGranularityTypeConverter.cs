// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for ClientGranularityType that handles both PascalCase and kebab-case strings.
/// </summary>
/// <remarks>
/// Unrecognised values fall back to <see cref="ClientGranularityType.PerArea"/> rather than throwing,
/// matching <see cref="GenerationModeTypeConverter"/>. PerArea is the historical behaviour, so an
/// unparseable marker file can never silently switch a project into single-client mode.
/// </remarks>
public class ClientGranularityTypeConverter : JsonConverter<ClientGranularityType>
{
    /// <inheritdoc />
    public override ClientGranularityType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.TokenType == JsonTokenType.String
            ? reader.GetString()
            : null;

        return value?.ToLowerInvariant() switch
        {
            "single" => ClientGranularityType.Single,
            "perarea" or "per-area" => ClientGranularityType.PerArea,
            _ => ClientGranularityType.PerArea, // Default fallback
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        ClientGranularityType value,
        JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            ClientGranularityType.Single => "Single",
            _ => "PerArea",
        };
        writer.WriteStringValue(stringValue);
    }
}