namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Represents a single generated file.
/// For source generator: passed to context.AddSource(FileName, Content)
/// For CLI tool: written to OutputDirectory/SubFolder/FileName
/// </summary>
/// <param name="FileName">The file name (e.g., "PetStore.Models.g.cs").</param>
/// <param name="Content">The complete file content (header + usings + namespace + body).</param>
/// <param name="SubFolder">Optional subfolder for organization (e.g., "Pets/Models").</param>
/// <param name="IsPhysicalFile">Whether this file should be written to disk (for domain handlers).</param>
public record GeneratedFile(
    string FileName,
    string Content,
    string? SubFolder = null,
    bool IsPhysicalFile = false);