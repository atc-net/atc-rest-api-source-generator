namespace Atc.Rest.Api.Generator.JsonConverters;

/// <summary>
/// JSON converter for SubFolderStrategyType that handles both PascalCase and kebab-case strings.
/// </summary>
public class SubFolderStrategyTypeConverter : MarkerValueConverter<SubFolderStrategyType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubFolderStrategyTypeConverter"/> class.
    /// </summary>
    public SubFolderStrategyTypeConverter()
        : base(
            SubFolderStrategyType.None,
            Value(SubFolderStrategyType.None, "None"),
            Value(SubFolderStrategyType.FirstPathSegment, "FirstPathSegment", "first-path-segment", "firstpathsegment"),
            Value(SubFolderStrategyType.OpenApiTag, "OpenApiTag", "openapi-tag", "openapitag"))
    {
    }
}