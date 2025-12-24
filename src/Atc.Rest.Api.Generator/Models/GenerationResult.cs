namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Result of a code generation operation.
/// Contains generated files and any diagnostics that occurred during generation.
/// </summary>
/// <param name="Files">The list of generated files.</param>
/// <param name="Success">Whether the generation was successful (no errors).</param>
/// <param name="Diagnostics">Any diagnostic messages produced during generation.</param>
public record GenerationResult(
    IReadOnlyList<GeneratedFile> Files,
    bool Success,
    IReadOnlyList<DiagnosticMessage> Diagnostics)
{
    /// <summary>
    /// Creates a successful result with the given files.
    /// </summary>
    public static GenerationResult Successful(
        IReadOnlyList<GeneratedFile> files)
        => new(files, Success: true, Diagnostics: Array.Empty<DiagnosticMessage>());

    /// <summary>
    /// Creates a successful result with the given files and diagnostics.
    /// </summary>
    public static GenerationResult Successful(
        IReadOnlyList<GeneratedFile> files,
        IReadOnlyList<DiagnosticMessage> diagnostics)
        => new(files, Success: true, diagnostics);

    /// <summary>
    /// Creates a failed result with the given diagnostics.
    /// </summary>
    public static GenerationResult Failed(
        IReadOnlyList<DiagnosticMessage> diagnostics)
        => new(Files: Array.Empty<GeneratedFile>(), Success: false, diagnostics);

    /// <summary>
    /// Creates a failed result with a single error message.
    /// </summary>
    public static GenerationResult Failed(
        string ruleId,
        string errorMessage,
        string? filePath = null)
        => new(
            Files: Array.Empty<GeneratedFile>(),
            Success: false,
            Diagnostics: new[] { new DiagnosticMessage(ruleId, errorMessage, DiagnosticSeverity.Error, filePath) });
}