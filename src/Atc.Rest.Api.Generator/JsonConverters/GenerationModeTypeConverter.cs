namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for GenerationModeType that handles both PascalCase and kebab-case strings.
/// </summary>
public class GenerationModeTypeConverter : MarkerValueConverter<GenerationModeType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationModeTypeConverter"/> class.
    /// </summary>
    public GenerationModeTypeConverter()
        : base(
            GenerationModeType.TypedClient,
            Value(GenerationModeType.TypedClient, "TypedClient", "typed-client"),
            Value(GenerationModeType.EndpointPerOperation, "EndpointPerOperation", "endpoint-per-operation"))
    {
    }
}