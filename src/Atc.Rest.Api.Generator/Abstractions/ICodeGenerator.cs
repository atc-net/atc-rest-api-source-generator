namespace Atc.Rest.Api.Generator.Abstractions;

/// <summary>
/// Core abstraction for code generators (server, client, domain).
/// Implemented by ApiServerGenerator, ApiClientGenerator, ApiServerDomainGenerator.
/// </summary>
public interface ICodeGenerator
{
    /// <summary>
    /// Generates code from an OpenAPI document and configuration.
    /// </summary>
    /// <param name="request">The generation request containing document and config.</param>
    /// <param name="diagnostics">Reporter for errors, warnings, and info messages.</param>
    /// <returns>The generation result with generated files and diagnostics.</returns>
    GenerationResult Generate(
        GenerationRequest request,
        IDiagnosticReporter diagnostics);
}