// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for ErrorResponseFormatType that handles both PascalCase and kebab-case strings.
/// </summary>
public class ErrorResponseFormatTypeConverter : JsonConverter<ErrorResponseFormatType>
{
    /// <inheritdoc />
    public override ErrorResponseFormatType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value?.ToLowerInvariant() switch
        {
            "problemdetails" or "problem-details" => ErrorResponseFormatType.ProblemDetails,
            "plaintext" or "plain-text" => ErrorResponseFormatType.PlainText,
            "plaintextonly" or "plain-text-only" => ErrorResponseFormatType.PlainTextOnly,
            "custom" => ErrorResponseFormatType.Custom,
            _ => ErrorResponseFormatType.ProblemDetails, // Default fallback
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        ErrorResponseFormatType value,
        JsonSerializerOptions options)
    {
        var stringValue = value switch
        {
            ErrorResponseFormatType.PlainText => "PlainText",
            ErrorResponseFormatType.PlainTextOnly => "PlainTextOnly",
            ErrorResponseFormatType.Custom => "Custom",
            _ => "ProblemDetails",
        };
        writer.WriteStringValue(stringValue);
    }
}