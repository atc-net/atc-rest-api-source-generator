namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Analysis of a path segment.
/// </summary>
public sealed class PathSegmentAnalysis
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PathSegmentAnalysis"/> class.
    /// </summary>
    public PathSegmentAnalysis(
        string segment,
        int pathCount,
        int operationCount,
        IReadOnlyList<string> schemas)
    {
        Segment = segment;
        PathCount = pathCount;
        OperationCount = operationCount;
        Schemas = schemas;
    }

    /// <summary>
    /// The path segment (e.g., "accounts", "users").
    /// </summary>
    public string Segment { get; }

    /// <summary>
    /// Number of paths in this segment.
    /// </summary>
    public int PathCount { get; }

    /// <summary>
    /// Number of operations in this segment.
    /// </summary>
    public int OperationCount { get; }

    /// <summary>
    /// Schemas primarily used by this segment.
    /// </summary>
    public IReadOnlyList<string> Schemas { get; }
}