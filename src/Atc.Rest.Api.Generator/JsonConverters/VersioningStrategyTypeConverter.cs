namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for VersioningStrategyType that handles both PascalCase and kebab-case strings.
/// </summary>
public class VersioningStrategyTypeConverter : MarkerValueConverter<VersioningStrategyType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersioningStrategyTypeConverter"/> class.
    /// </summary>
    public VersioningStrategyTypeConverter()
        : base(
            VersioningStrategyType.None,
            Value(VersioningStrategyType.None, "None"),
            Value(VersioningStrategyType.QueryString, "QueryString", "query-string"),
            Value(VersioningStrategyType.UrlSegment, "UrlSegment", "url-segment"),
            Value(VersioningStrategyType.Header, "Header"))
    {
    }
}