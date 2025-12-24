namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Content for a split file.
/// </summary>
public sealed class SplitFileContent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SplitFileContent"/> class.
    /// </summary>
    public SplitFileContent(
        string fileName,
        string content,
        string? partName = null,
        bool isBaseFile = false,
        bool isCommonFile = false,
        int pathCount = 0,
        int schemaCount = 0,
        int parameterCount = 0)
    {
        FileName = fileName;
        Content = content;
        PartName = partName;
        IsBaseFile = isBaseFile;
        IsCommonFile = isCommonFile;
        PathCount = pathCount;
        SchemaCount = schemaCount;
        ParameterCount = parameterCount;
    }

    /// <summary>
    /// The suggested file name.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// The YAML content for this file.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// The part name (e.g., "Accounts", "Users", "Common").
    /// Null for the base file.
    /// </summary>
    public string? PartName { get; }

    /// <summary>
    /// Whether this is the base file.
    /// </summary>
    public bool IsBaseFile { get; }

    /// <summary>
    /// Whether this is the common file (shared schemas).
    /// </summary>
    public bool IsCommonFile { get; }

    /// <summary>
    /// Number of paths in this file.
    /// </summary>
    public int PathCount { get; }

    /// <summary>
    /// Number of schemas in this file.
    /// </summary>
    public int SchemaCount { get; }

    /// <summary>
    /// Number of parameters in this file.
    /// </summary>
    public int ParameterCount { get; }

    /// <summary>
    /// Estimated line count.
    /// </summary>
    public int EstimatedLines => Content.Split('\n').Length;
}