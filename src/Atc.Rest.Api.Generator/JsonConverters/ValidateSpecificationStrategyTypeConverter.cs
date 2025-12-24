namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for ValidateSpecificationStrategy enum supporting both PascalCase and kebab-case.
/// </summary>
public class ValidateSpecificationStrategyTypeConverter : JsonConverter<ValidateSpecificationStrategy>
{
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