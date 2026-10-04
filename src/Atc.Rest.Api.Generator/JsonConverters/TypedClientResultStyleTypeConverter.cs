namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for TypedClientResultStyleType (case-insensitive).
/// </summary>
/// <remarks>
/// Unrecognised values fall back to <see cref="TypedClientResultStyleType.Throw"/> rather than throwing,
/// matching <see cref="ClientGranularityTypeConverter"/>. Throw is the historical behaviour, so an
/// unparseable marker file can never silently change the shape of every generated operation.
/// </remarks>
public class TypedClientResultStyleTypeConverter : MarkerValueConverter<TypedClientResultStyleType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TypedClientResultStyleTypeConverter"/> class.
    /// </summary>
    public TypedClientResultStyleTypeConverter()
        : base(
            TypedClientResultStyleType.Throw,
            Value(TypedClientResultStyleType.Result, "Result"),
            Value(TypedClientResultStyleType.Throw, "Throw"))
    {
    }
}