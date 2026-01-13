namespace Atc.Rest.Api.Generator.Models;

/// <summary>
/// Statistics collected during code generation for reporting purposes.
/// </summary>
public record GenerationStatistics
{
    /// <summary>
    /// The name of the OpenAPI specification file.
    /// </summary>
    public string SpecificationName { get; init; } = string.Empty;

    /// <summary>
    /// The API version from the OpenAPI info section.
    /// </summary>
    public string SpecificationVersion { get; init; } = string.Empty;

    /// <summary>
    /// The OpenAPI specification version (e.g., "3.1.1").
    /// </summary>
    public string OpenApiVersion { get; init; } = string.Empty;

    /// <summary>
    /// The API title from the OpenAPI info section.
    /// </summary>
    public string ApiTitle { get; init; } = string.Empty;

    /// <summary>
    /// The version of the generator.
    /// </summary>
    public string GeneratorVersion { get; init; } = string.Empty;

    /// <summary>
    /// The type of generator ("Server" or "Client").
    /// </summary>
    public string GeneratorType { get; init; } = string.Empty;

    /// <summary>
    /// The timestamp when generation occurred.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The duration of the generation process.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Number of model records generated.
    /// </summary>
    public int ModelsCount { get; init; }

    /// <summary>
    /// Number of enum types generated.
    /// </summary>
    public int EnumsCount { get; init; }

    /// <summary>
    /// Number of parameter classes generated.
    /// </summary>
    public int ParametersCount { get; init; }

    /// <summary>
    /// Number of result classes generated.
    /// </summary>
    public int ResultsCount { get; init; }

    /// <summary>
    /// Number of handler interfaces generated.
    /// </summary>
    public int HandlersCount { get; init; }

    /// <summary>
    /// Number of endpoint registration files generated.
    /// </summary>
    public int EndpointsCount { get; init; }

    /// <summary>
    /// Total number of operations in the OpenAPI specification.
    /// </summary>
    public int OperationsCount { get; init; }

    /// <summary>
    /// Number of client methods generated (TypedClient mode).
    /// </summary>
    public int ClientMethodsCount { get; init; }

    /// <summary>
    /// Number of endpoint classes generated (EndpointPerOperation mode).
    /// </summary>
    public int EndpointClassesCount { get; init; }

    /// <summary>
    /// Number of webhooks defined in the OpenAPI specification (OpenAPI 3.1 feature).
    /// </summary>
    public int WebhooksCount { get; init; }

    /// <summary>
    /// Number of webhook handlers generated (OpenAPI 3.1 feature).
    /// </summary>
    public int WebhookHandlersCount { get; init; }

    /// <summary>
    /// Number of validation errors.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Number of validation warnings.
    /// </summary>
    public int WarningCount { get; init; }

    /// <summary>
    /// List of error rule IDs encountered.
    /// </summary>
    public IReadOnlyList<string> ErrorRuleIds { get; init; } = [];

    /// <summary>
    /// List of warning rule IDs encountered.
    /// </summary>
    public IReadOnlyList<string> WarningRuleIds { get; init; } = [];

    /// <summary>
    /// The project structure type (e.g., "SingleProject", "TwoProjects", "ThreeProjects").
    /// </summary>
    public string ProjectStructure { get; init; } = string.Empty;

    /// <summary>
    /// List of project names created.
    /// </summary>
    public IReadOnlyList<string> ProjectsCreated { get; init; } = [];

    /// <summary>
    /// List of files created.
    /// </summary>
    public IReadOnlyList<string> FilesCreated { get; init; } = [];

    /// <summary>
    /// Total number of types generated across all categories.
    /// </summary>
    public int TotalTypesGenerated =>
        ModelsCount +
        EnumsCount +
        ParametersCount +
        ResultsCount +
        HandlersCount +
        EndpointsCount +
        ClientMethodsCount +
        EndpointClassesCount +
        WebhookHandlersCount;
}