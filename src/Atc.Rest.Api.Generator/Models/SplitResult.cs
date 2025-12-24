namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Result of splitting an OpenAPI specification into multiple files.
/// </summary>
public sealed class SplitResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SplitResult"/> class.
    /// </summary>
    public SplitResult(
        SplitFileContent baseFile,
        IReadOnlyList<SplitFileContent> partFiles,
        SplitFileContent? commonFile,
        IReadOnlyList<DiagnosticMessage> diagnostics,
        SplitStrategy strategy)
    {
        BaseFile = baseFile;
        PartFiles = partFiles;
        CommonFile = commonFile;
        Diagnostics = diagnostics;
        Strategy = strategy;
    }

    /// <summary>
    /// The base file content (info, servers, security).
    /// </summary>
    public SplitFileContent BaseFile { get; }

    /// <summary>
    /// The part files content (paths and schemas per domain).
    /// </summary>
    public IReadOnlyList<SplitFileContent> PartFiles { get; }

    /// <summary>
    /// The common file content (shared schemas and parameters).
    /// May be null if no common schemas were extracted.
    /// </summary>
    public SplitFileContent? CommonFile { get; }

    /// <summary>
    /// All generated files (base + parts + common).
    /// </summary>
    public IReadOnlyList<SplitFileContent> AllFiles
    {
        get
        {
            var files = new List<SplitFileContent> { BaseFile };
            files.AddRange(PartFiles);
            if (CommonFile != null)
            {
                files.Add(CommonFile);
            }

            return files;
        }
    }

    /// <summary>
    /// Diagnostics generated during split.
    /// </summary>
    public IReadOnlyList<DiagnosticMessage> Diagnostics { get; }

    /// <summary>
    /// Whether the split was successful.
    /// </summary>
    public bool IsSuccess
        => Diagnostics.All(d => d.Severity != DiagnosticSeverity.Error);

    /// <summary>
    /// The split strategy that was used.
    /// </summary>
    public SplitStrategy Strategy { get; }
}