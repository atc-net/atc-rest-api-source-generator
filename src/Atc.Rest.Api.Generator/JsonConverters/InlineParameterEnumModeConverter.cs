namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for <see cref="InlineParameterEnumMode"/> accepting "Enum" or "String"
/// (case-insensitive). Unknown values fall back to <see cref="InlineParameterEnumMode.Enum"/>.
/// </summary>
public class InlineParameterEnumModeConverter : MarkerValueConverter<InlineParameterEnumMode>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InlineParameterEnumModeConverter"/> class.
    /// </summary>
    public InlineParameterEnumModeConverter()
        : base(
            InlineParameterEnumMode.Enum,
            Value(InlineParameterEnumMode.Enum, "Enum"),
            Value(InlineParameterEnumMode.String, "String"))
    {
    }
}