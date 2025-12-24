namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Analysis of a single tag.
/// </summary>
public sealed class TagAnalysis
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TagAnalysis"/> class.
    /// </summary>
    public TagAnalysis(
        string name,
        int operationCount,
        int schemaCount,
        IReadOnlyList<string> paths)
    {
        Name = name;
        OperationCount = operationCount;
        SchemaCount = schemaCount;
        Paths = paths;
    }

    /// <summary>
    /// The tag name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Number of operations with this tag.
    /// </summary>
    public int OperationCount { get; }

    /// <summary>
    /// Number of schemas primarily used by this tag.
    /// </summary>
    public int SchemaCount { get; }

    /// <summary>
    /// Paths that have operations with this tag.
    /// </summary>
    public IReadOnlyList<string> Paths { get; }
}