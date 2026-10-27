namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// A specification as <see cref="Services.SpecificationPipeline"/> read it.
/// </summary>
/// <param name="Document">The document, or <see langword="null"/> when it could not be parsed.</param>
/// <param name="Diagnostics">The parser's errors, or the validation's diagnostics.</param>
public sealed record SpecificationPipelineResult(
    OpenApiDocument? Document,
    List<DiagnosticMessage> Diagnostics)
{
    /// <summary>
    /// Gets a value indicating whether code is generated from the document: it was parsed and has no error.
    /// </summary>
    public bool IsValid
        => Document is not null && !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}