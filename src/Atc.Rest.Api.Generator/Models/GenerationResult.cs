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
}