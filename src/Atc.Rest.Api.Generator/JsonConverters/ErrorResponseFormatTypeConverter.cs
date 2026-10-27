namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for ErrorResponseFormatType that handles both PascalCase and kebab-case strings.
/// </summary>
public class ErrorResponseFormatTypeConverter : MarkerValueConverter<ErrorResponseFormatType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorResponseFormatTypeConverter"/> class.
    /// </summary>
    public ErrorResponseFormatTypeConverter()
        : base(
            ErrorResponseFormatType.ProblemDetails,
            Value(ErrorResponseFormatType.ProblemDetails, "ProblemDetails", "problem-details"),
            Value(ErrorResponseFormatType.PlainText, "PlainText", "plain-text"),
            Value(ErrorResponseFormatType.PlainTextOnly, "PlainTextOnly", "plain-text-only"),
            Value(ErrorResponseFormatType.Custom, "Custom"))
    {
    }
}