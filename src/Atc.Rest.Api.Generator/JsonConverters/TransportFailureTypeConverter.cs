namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for TransportFailureType (case-insensitive); an unrecognised value is <see cref="TransportFailureType.Throw"/>.
/// </summary>
public class TransportFailureTypeConverter : MarkerValueConverter<TransportFailureType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransportFailureTypeConverter"/> class.
    /// </summary>
    public TransportFailureTypeConverter()
        : base(
            TransportFailureType.Throw,
            Value(TransportFailureType.Result, "Result", "result"),
            Value(TransportFailureType.Throw, "Throw", "throw"))
    {
    }
}