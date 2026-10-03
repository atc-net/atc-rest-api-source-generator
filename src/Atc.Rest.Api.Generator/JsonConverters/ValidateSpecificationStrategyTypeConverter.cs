namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for ValidateSpecificationStrategy enum supporting both PascalCase and kebab-case.
/// </summary>
public class ValidateSpecificationStrategyTypeConverter : JsonConverter<ValidateSpecificationStrategy>
{
    /// <summary>
    /// Reads the strategy from its PascalCase (<c>Strict</c>) or kebab-case (<c>strict</c>, <c>atc-rules</c>) name.
    /// </summary>
    /// <param name="reader">The reader, at the string.</param>
    /// <param name="typeToConvert">The enum type.</param>
    /// <param name="options">The serializer options.</param>
    /// <returns>The strategy.</returns>
    public override ValidateSpecificationStrategy Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidateSpecificationStrategy.Standard; // Default
        }

        return value!.ToLowerInvariant() switch
        {
            "none" => ValidateSpecificationStrategy.None,
            "standard" => ValidateSpecificationStrategy.Standard,
            "strict" => ValidateSpecificationStrategy.Strict,
            _ => ValidateSpecificationStrategy.Standard, // Default for unknown values
        };
    }

    /// <summary>
    /// Writes the strategy as its PascalCase name.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The strategy.</param>
    /// <param name="options">The serializer options.</param>
    public override void Write(
        Utf8JsonWriter writer,
        ValidateSpecificationStrategy value,
        JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            ValidateSpecificationStrategy.None => "none",
            ValidateSpecificationStrategy.Standard => "standard",
            ValidateSpecificationStrategy.Strict => "strict",
            _ => "standard",
        };

        writer.WriteStringValue(stringValue);
    }
}