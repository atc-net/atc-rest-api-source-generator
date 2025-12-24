namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Result of merging multiple OpenAPI specification files.
/// </summary>
public sealed class MergeResult
{
    private MergeResult(
        OpenApiDocument? document,
        SpecificationFile? baseFile,
        IReadOnlyList<SpecificationFile> partFiles,
        IReadOnlyList<DiagnosticMessage> diagnostics)
    {
        Document = document;
        BaseFile = baseFile;
        PartFiles = partFiles;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// The merged OpenAPI document (null if merge failed).
    /// </summary>
    public OpenApiDocument? Document { get; }

    /// <summary>
    /// The base file that was merged into.
    /// </summary>
    public SpecificationFile? BaseFile { get; }

    /// <summary>
    /// List of part files that were merged.
    /// </summary>
    public IReadOnlyList<SpecificationFile> PartFiles { get; }

    /// <summary>
    /// All source files that were merged (base + parts).
    /// </summary>
    public IReadOnlyList<SpecificationFile> AllFiles
    {
        get
        {
            if (BaseFile == null)
            {
                return PartFiles;
            }

            var files = new List<SpecificationFile> { BaseFile };
            files.AddRange(PartFiles);
            return files;
        }
    }

    /// <summary>
    /// Diagnostics generated during merge.
    /// </summary>
    public IReadOnlyList<DiagnosticMessage> Diagnostics { get; }

    /// <summary>
    /// Whether the merge was successful (no errors).
    /// </summary>
    public bool IsSuccess
        => Document != null && !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Whether this was a multi-part merge (more than one file).
    /// </summary>
    public bool IsMultiPart => PartFiles.Count > 0;

    /// <summary>
    /// Total number of paths in the merged document.
    /// </summary>
    public int TotalPaths => Document?.Paths?.Count ?? 0;

    /// <summary>
    /// Total number of schemas in the merged document.
    /// </summary>
    public int TotalSchemas => Document?.Components?.Schemas?.Count ?? 0;

    /// <summary>
    /// Total number of operations in the merged document.
    /// </summary>
    public int TotalOperations => Document?.Paths?
        .Sum(p => p.Value?.Operations?.Count ?? 0) ?? 0;

    /// <summary>
    /// Creates a successful merge result.
    /// </summary>
    public static MergeResult Success(
        OpenApiDocument document,
        SpecificationFile baseFile,
        IReadOnlyList<SpecificationFile> partFiles,
        IReadOnlyList<DiagnosticMessage> diagnostics)
        => new(document, baseFile, partFiles, diagnostics);

    /// <summary>
    /// Creates a successful single-file result.
    /// </summary>
    public static MergeResult SingleFile(SpecificationFile file)
        => new(file.Document, file, Array.Empty<SpecificationFile>(), Array.Empty<DiagnosticMessage>());

    /// <summary>
    /// Creates a failed result with diagnostics.
    /// </summary>
    public static MergeResult Failed(
        IReadOnlyList<DiagnosticMessage> diagnostics)
        => new(null, null, Array.Empty<SpecificationFile>(), diagnostics);

    /// <summary>
    /// Creates a failed result with a single error message.
    /// </summary>
    public static MergeResult Failed(
        string ruleId,
        string message,
        string? filePath = null)
        => Failed([DiagnosticMessage.Error(ruleId, message, filePath)]);
}