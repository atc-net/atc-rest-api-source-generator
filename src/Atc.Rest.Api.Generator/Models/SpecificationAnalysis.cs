namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Analysis result for an OpenAPI specification.
/// Used for recommending split strategy.
/// </summary>
public sealed class SpecificationAnalysis
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpecificationAnalysis"/> class.
    /// </summary>
    public SpecificationAnalysis(
        string filePath,
        int totalLines,
        int totalPaths,
        int totalOperations,
        int totalSchemas,
        int totalParameters,
        IReadOnlyDictionary<string, TagAnalysis> tags,
        IReadOnlyDictionary<string, PathSegmentAnalysis> pathSegments,
        IReadOnlyList<SharedSchemaAnalysis> sharedSchemas,
        SplitStrategy recommendedStrategy,
        string recommendedStrategyReason,
        IReadOnlyList<SuggestedSplit> suggestedSplits)
    {
        FilePath = filePath;
        TotalLines = totalLines;
        TotalPaths = totalPaths;
        TotalOperations = totalOperations;
        TotalSchemas = totalSchemas;
        TotalParameters = totalParameters;
        Tags = tags;
        PathSegments = pathSegments;
        SharedSchemas = sharedSchemas;
        RecommendedStrategy = recommendedStrategy;
        RecommendedStrategyReason = recommendedStrategyReason;
        SuggestedSplits = suggestedSplits;
    }

    /// <summary>
    /// The file path that was analyzed.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Total line count of the specification.
    /// </summary>
    public int TotalLines { get; }

    /// <summary>
    /// Total number of paths.
    /// </summary>
    public int TotalPaths { get; }

    /// <summary>
    /// Total number of operations.
    /// </summary>
    public int TotalOperations { get; }

    /// <summary>
    /// Total number of schemas.
    /// </summary>
    public int TotalSchemas { get; }

    /// <summary>
    /// Total number of parameters.
    /// </summary>
    public int TotalParameters { get; }

    /// <summary>
    /// Tags found in the specification with their operation counts.
    /// </summary>
    public IReadOnlyDictionary<string, TagAnalysis> Tags { get; }

    /// <summary>
    /// Path segments found with their path and operation counts.
    /// </summary>
    public IReadOnlyDictionary<string, PathSegmentAnalysis> PathSegments { get; }

    /// <summary>
    /// Schemas that are shared across multiple domains.
    /// </summary>
    public IReadOnlyList<SharedSchemaAnalysis> SharedSchemas { get; }

    /// <summary>
    /// The recommended split strategy.
    /// </summary>
    public SplitStrategy RecommendedStrategy { get; }

    /// <summary>
    /// Reason for the recommended strategy.
    /// </summary>
    public string RecommendedStrategyReason { get; }

    /// <summary>
    /// Suggested split configuration.
    /// </summary>
    public IReadOnlyList<SuggestedSplit> SuggestedSplits { get; }

    /// <summary>
    /// Whether the specification would benefit from splitting.
    /// </summary>
    public bool ShouldSplit
        => TotalLines > 500 || TotalOperations > 15 || TotalSchemas > 20;
}