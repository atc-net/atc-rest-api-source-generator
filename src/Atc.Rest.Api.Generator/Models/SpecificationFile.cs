namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Represents a single OpenAPI specification file.
/// </summary>
public sealed class SpecificationFile
{
    /// <summary>
    /// The full path to the file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// The file name without path.
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// The file name without extension.
    /// </summary>
    public string FileNameWithoutExtension
        => Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>
    /// The YAML content of the file.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// The parsed OpenAPI document (may be null if parsing failed).
    /// </summary>
    public OpenApiDocument? Document { get; }

    /// <summary>
    /// Whether this is the base file (contains openapi version, info, servers).
    /// </summary>
    public bool IsBaseFile { get; }

    /// <summary>
    /// Whether this is a part file (contains paths and schemas for a domain).
    /// </summary>
    public bool IsPartFile { get; }

    /// <summary>
    /// The part name extracted from the file name (e.g., "Accounts" from "Showcase_Accounts.yaml").
    /// </summary>
    public string? PartName { get; }

    /// <summary>
    /// Number of paths defined in this file.
    /// </summary>
    public int PathCount => Document?.Paths?.Count ?? 0;

    /// <summary>
    /// Number of schemas defined in this file.
    /// </summary>
    public int SchemaCount => Document?.Components?.Schemas?.Count ?? 0;

    /// <summary>
    /// Number of parameters defined in this file.
    /// </summary>
    public int ParameterCount => Document?.Components?.Parameters?.Count ?? 0;

    /// <summary>
    /// Number of operations defined in this file.
    /// </summary>
    public int OperationCount => Document?.Paths?
        .Sum(p => p.Value?.Operations?.Count ?? 0) ?? 0;

    /// <summary>
    /// Gets the tags defined in this file.
    /// </summary>
    /// <returns>The list of tag names.</returns>
    public IReadOnlyList<string> GetTags()
    {
        if (Document?.Tags == null)
        {
            return Array.Empty<string>();
        }

        return Document.Tags
            .Select(t => t.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Cast<string>()
            .ToList();
    }

    private SpecificationFile(
        string filePath,
        string content,
        OpenApiDocument? document,
        bool isBaseFile,
        bool isPartFile,
        string? partName)
    {
        FilePath = filePath;
        Content = content;
        Document = document;
        IsBaseFile = isBaseFile;
        IsPartFile = isPartFile;
        PartName = partName;
    }

    /// <summary>
    /// Creates a SpecificationFile from a file path.
    /// </summary>
    public static SpecificationFile FromFile(
        string filePath,
        string? baseName = null)
    {
        var content = File.ReadAllText(filePath);
        return FromContent(filePath, content, baseName);
    }

    /// <summary>
    /// Creates a SpecificationFile from content.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "OK.")]
    public static SpecificationFile FromContent(
        string filePath,
        string content,
        string? baseName = null)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var isBase = baseName == null || fileName.Equals(baseName, StringComparison.OrdinalIgnoreCase);
        var isPart = !isBase && fileName.StartsWith($"{baseName}_", StringComparison.OrdinalIgnoreCase);
        var partName = isPart ? fileName.Substring(baseName!.Length + 1) : null;

        OpenApiDocument? document = null;

        try
        {
            document = OpenApiDocumentHelper.ParseYaml(content);
        }
        catch
        {
            // Document parsing failed, will be reported as diagnostic
        }

        return new SpecificationFile(filePath, content, document, isBase, isPart, partName);
    }

    /// <summary>
    /// Creates an empty SpecificationFile for error cases.
    /// </summary>
    public static SpecificationFile Empty(string filePath)
        => new(
            filePath,
            string.Empty,
            null,
            true,
            false,
            null);
}