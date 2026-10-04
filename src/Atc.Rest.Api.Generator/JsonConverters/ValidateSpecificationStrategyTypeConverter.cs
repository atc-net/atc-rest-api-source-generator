namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for ValidateSpecificationStrategy (case-insensitive); an unknown value is <c>standard</c>.
/// </summary>
public class ValidateSpecificationStrategyTypeConverter : MarkerValueConverter<ValidateSpecificationStrategy>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateSpecificationStrategyTypeConverter"/> class.
    /// </summary>
    public ValidateSpecificationStrategyTypeConverter()
        : base(
            ValidateSpecificationStrategy.Standard,
            Value(ValidateSpecificationStrategy.None, "none"),
            Value(ValidateSpecificationStrategy.Standard, "standard"),
            Value(ValidateSpecificationStrategy.Strict, "strict"))
    {
    }
}