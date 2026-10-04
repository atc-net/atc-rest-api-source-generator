namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for ClientGranularityType that handles both PascalCase and kebab-case strings.
/// </summary>
/// <remarks>
/// Unrecognised values fall back to <see cref="ClientGranularityType.PerArea"/> rather than throwing,
/// matching <see cref="GenerationModeTypeConverter"/>. PerArea is the historical behaviour, so an
/// unparseable marker file can never silently switch a project into single-client mode.
/// </remarks>
public class ClientGranularityTypeConverter : MarkerValueConverter<ClientGranularityType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientGranularityTypeConverter"/> class.
    /// </summary>
    public ClientGranularityTypeConverter()
        : base(
            ClientGranularityType.PerArea,
            Value(ClientGranularityType.Single, "Single"),
            Value(ClientGranularityType.PerArea, "PerArea", "per-area"))
    {
    }
}