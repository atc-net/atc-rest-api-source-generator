// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for GenerationModeType that handles both PascalCase and kebab-case strings.
/// </summary>
public class GenerationModeTypeConverter : JsonConverter<GenerationModeType>
{
    /// <inheritdoc />
    public override GenerationModeType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "typedclient" or "typed-client" => GenerationModeType.TypedClient,
            "endpointperoperation" or "endpoint-per-operation" => GenerationModeType.EndpointPerOperation,
            _ => GenerationModeType.TypedClient, // Default fallback
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        GenerationModeType value,
        JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            GenerationModeType.EndpointPerOperation => "EndpointPerOperation",
            _ => "TypedClient",
        };
        writer.WriteStringValue(stringValue);
    }
}