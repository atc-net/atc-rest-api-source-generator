namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Configuration for multi-part OpenAPI specification support.
/// Extracted from x-multipart extension in the base file.
/// </summary>
public sealed record MultiPartConfiguration
{
    /// <summary>
    /// Whether multi-part support is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Discovery mode: "auto" or "explicit".
    /// Auto: Discovers part files by naming convention ({BaseName}_{PartName}.yaml).
    /// Explicit: Uses the Parts list for specific ordering.
    /// </summary>
    [MarkerValues("auto", "explicit")]
    public string Discovery { get; init; } = "auto";

    /// <summary>
    /// Explicit list of part files (when Discovery = "explicit").
    /// Paths are relative to the base file.
    /// </summary>
    public IReadOnlyList<string> Parts { get; init; } = [];

    /// <summary>
    /// Merge strategy for paths.
    /// </summary>
    [JsonConverter(typeof(MergeStrategyConverter))]
    public MergeStrategy PathsMergeStrategy { get; init; } = MergeStrategy.ErrorOnDuplicate;

    /// <summary>
    /// Merge strategy for schemas.
    /// </summary>
    [JsonConverter(typeof(MergeStrategyConverter))]
    public MergeStrategy SchemasMergeStrategy { get; init; } = MergeStrategy.ErrorOnDuplicate;

    /// <summary>
    /// Merge strategy for parameters.
    /// </summary>
    [JsonConverter(typeof(MergeStrategyConverter))]
    public MergeStrategy ParametersMergeStrategy { get; init; } = MergeStrategy.MergeIfIdentical;

    /// <summary>
    /// Creates a default configuration with auto-discovery enabled.
    /// </summary>
    public static MultiPartConfiguration Default { get; } = new();
}