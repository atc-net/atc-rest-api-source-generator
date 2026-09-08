namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for TypedClientResultStyleType that handles both PascalCase and kebab-case strings.
/// </summary>
/// <remarks>
/// Unrecognised values fall back to <see cref="TypedClientResultStyleType.Throw"/> rather than throwing,
/// matching <see cref="ClientGranularityTypeConverter"/>. Throw is the historical behaviour, so an
/// unparseable marker file can never silently change the shape of every generated operation.
/// </remarks>
public class TypedClientResultStyleTypeConverter : JsonConverter<TypedClientResultStyleType>
{
    /// <inheritdoc />
    public override TypedClientResultStyleType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.TokenType == JsonTokenType.String
            ? reader.GetString()
            : null;

        return value?.ToLowerInvariant() switch
        {
            "result" => TypedClientResultStyleType.Result,
            "throw" => TypedClientResultStyleType.Throw,
            _ => TypedClientResultStyleType.Throw, // Default fallback
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        TypedClientResultStyleType value,
        JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            TypedClientResultStyleType.Result => "Result",
            _ => "Throw",
        };
        writer.WriteStringValue(stringValue);
    }
}