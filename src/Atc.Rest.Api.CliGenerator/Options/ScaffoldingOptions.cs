namespace Atc.Rest.Api.CliGenerator.Options;

/// <summary>
/// Repository scaffolding configuration options.
/// </summary>
public sealed class ScaffoldingOptions
{
    /// <summary>
    /// Project structure type for code generation.
    /// Default: ThreeProjects (3 separate projects).
    /// </summary>
    public ProjectStructureType ProjectStructure { get; set; } = ProjectStructureType.ThreeProjects;

    /// <summary>
    /// Skip adding ATC coding rules updater files.
    /// Default: false (coding rules are added by default).
    /// </summary>
    public bool NoCodingRules { get; set; }

    /// <summary>
    /// Test framework to use for generated test project.
    /// Default: "xunit".
    /// </summary>
    public string TestFramework { get; set; } = "xunit";

    /// <summary>
    /// Target framework for generated projects.
    /// Default: "net10.0".
    /// </summary>
    public string TargetFramework { get; set; } = "net10.0";

    /// <summary>
    /// Override host project name (ThreeProjects mode only).
    /// Default: null (auto-derived from base name).
    /// </summary>
    public string? HostProjectName { get; set; }

    /// <summary>
    /// Override contracts project name.
    /// Default: null (auto-derived from base name).
    /// </summary>
    public string? ContractsProjectName { get; set; }

    /// <summary>
    /// Override domain project name (TwoProjects/ThreeProjects mode only).
    /// Default: null (auto-derived from base name).
    /// </summary>
    public string? DomainProjectName { get; set; }

    /// <summary>
    /// Override client project name.
    /// Default: null (auto-derived from base name).
    /// </summary>
    public string? ClientProjectName { get; set; }

    /// <summary>
    /// Override host namespace (for generated code).
    /// Default: null (auto-derived from project name).
    /// </summary>
    public string? HostNamespace { get; set; }

    /// <summary>
    /// Override contracts namespace (written to marker file).
    /// Default: null (auto-derived from project name).
    /// </summary>
    public string? ContractsNamespace { get; set; }

    /// <summary>
    /// Override domain namespace (written to marker file).
    /// Default: null (auto-derived from project name).
    /// </summary>
    public string? DomainNamespace { get; set; }

    /// <summary>
    /// Override client namespace (written to marker file).
    /// Default: null (auto-derived from project name).
    /// </summary>
    public string? ClientNamespace { get; set; }

    /// <summary>
    /// API documentation UI provider.
    /// Default: Scalar.
    /// </summary>
    public HostUiType HostUi { get; set; } = HostUiType.Scalar;

    /// <summary>
    /// Controls when the API documentation UI is enabled.
    /// Default: DevelopmentOnly.
    /// </summary>
    public HostUiModeType HostUiMode { get; set; } = HostUiModeType.DevelopmentOnly;

    /// <summary>
    /// Create an Aspire AppHost project for orchestration.
    /// Default: false.
    /// </summary>
    public bool Aspire { get; set; }
}