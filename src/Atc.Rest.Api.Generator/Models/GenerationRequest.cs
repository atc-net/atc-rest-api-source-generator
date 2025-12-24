namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Input to ICodeGenerator.Generate().
/// Contains everything needed to generate code from an OpenAPI specification.
/// </summary>
/// <param name="Document">The parsed OpenAPI document.</param>
/// <param name="Config">The generator configuration (ServerConfig, ClientConfig, or ServerDomainConfig).</param>
/// <param name="ProjectName">The project name used for namespaces and file naming.</param>
/// <param name="OutputDirectory">Optional physical output path (CLI only, null for source generator).</param>
/// <param name="HandlerScanner">Optional scanner for finding existing handler implementations.</param>
public record GenerationRequest(
    OpenApiDocument Document,
    BaseConfig Config,
    string ProjectName,
    string? OutputDirectory = null,
    IHandlerScanner? HandlerScanner = null);