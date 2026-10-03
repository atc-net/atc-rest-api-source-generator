namespace Atc.Rest.Api.Generator.Cli.Commands.Settings;

/// <summary>
/// Settings for the generate server command.
/// </summary>
public sealed class GenerateServerCommandSettings : BaseGenerateCommandSettings
{
    /// <summary>
    /// Gets how generated files are grouped into sub-folders: <c>None</c>, <c>FirstPathSegment</c> or <c>OpenApiTag</c>.
    /// </summary>
    [CommandOption("--sub-folder-strategy <STRATEGY>")]
    [Description("Sub-folder organization strategy: None, FirstPathSegment, or OpenApiTag.")]
    public string? SubFolderStrategy { get; init; }

    /// <summary>
    /// Gets how the API is versioned: <c>None</c>, <c>QueryString</c>, <c>UrlSegment</c> or <c>Header</c>.
    /// </summary>
    [CommandOption("--versioning-strategy <STRATEGY>")]
    [Description("API versioning strategy: None, QueryString, UrlSegment or Header.")]
    public string? VersioningStrategy { get; init; }

    /// <summary>
    /// Gets the API version used when a request names none, in <c>major.minor</c> form.
    /// </summary>
    [CommandOption("--default-api-version <VERSION>")]
    [Description("Default API version (e.g., '1.0').")]
    public string? DefaultApiVersion { get; init; }

    /// <summary>
    /// Gets or sets the output directory for the domain handler scaffolds (TwoProjects/ThreeProjects only); resolved to an absolute path by <see cref="Validate"/>.
    /// </summary>
    [CommandOption("--domain-output <PATH>")]
    [Description("Output path for domain handler scaffolds (TwoProjects/ThreeProjects mode only).")]
    public string? DomainOutputPath { get; set; }

    /// <summary>
    /// Gets the suffix appended to the generated handler class names (default <c>Handler</c>).
    /// </summary>
    [CommandOption("--handler-suffix <SUFFIX>")]
    [Description("Suffix for handler class names (default: 'Handler').")]
    public string? HandlerSuffix { get; init; }

    /// <summary>
    /// Gets the body of the scaffolded handlers: <c>throw-not-implemented</c>, <c>error-501</c> or <c>default-value</c>.
    /// </summary>
    [CommandOption("--stub-implementation <TYPE>")]
    [Description("Stub implementation type: throw-not-implemented, error-501, or default-value.")]
    public string? StubImplementation { get; init; }

    /// <summary>
    /// Gets the project layout: <c>SingleProject</c>, <c>TwoProjects</c> (Host+Contracts, Domain) or <c>ThreeProjects</c> (default).
    /// </summary>
    [CommandOption("--project-structure <TYPE>")]
    [Description("Project structure: SingleProject (1 project), TwoProjects (Host+Contracts, Domain), or ThreeProjects (3 projects, default).")]
    public string? ProjectStructure { get; init; }

    /// <summary>
    /// Gets a value indicating whether the ATC coding rules updater files are left out of the scaffolded solution.
    /// </summary>
    [CommandOption("--no-coding-rules")]
    [Description("Skip adding ATC coding rules updater files (added by default).")]
    [DefaultValue(false)]
    public bool NoCodingRules { get; init; }

    /// <summary>
    /// Gets the host project name override (ThreeProjects only); derived from the base project name when not set.
    /// </summary>
    [CommandOption("--host-project <NAME>")]
    [Description("Override the host project name (ThreeProjects mode only).")]
    public string? HostProjectName { get; init; }

    /// <summary>
    /// Gets the contracts project name override; derived from the base project name when not set.
    /// </summary>
    [CommandOption("--contracts-project <NAME>")]
    [Description("Override the contracts project name.")]
    public string? ContractsProjectName { get; init; }

    /// <summary>
    /// Gets the domain project name override (TwoProjects/ThreeProjects only); derived from the base project name when not set.
    /// </summary>
    [CommandOption("--domain-project <NAME>")]
    [Description("Override the domain project name (TwoProjects/ThreeProjects mode only).")]
    public string? DomainProjectName { get; init; }

    /// <summary>
    /// Gets the host namespace override used in the generated Program.cs and host code.
    /// </summary>
    [CommandOption("--host-namespace <NAMESPACE>")]
    [Description("Override the host namespace.")]
    public string? HostNamespace { get; init; }

    /// <summary>
    /// Gets the contracts namespace override written to the server marker file.
    /// </summary>
    [CommandOption("--contracts-namespace <NAMESPACE>")]
    [Description("Override the contracts namespace (written to marker file).")]
    public string? ContractsNamespace { get; init; }

    /// <summary>
    /// Gets the domain namespace override written to the handlers marker file.
    /// </summary>
    [CommandOption("--domain-namespace <NAMESPACE>")]
    [Description("Override the domain namespace (written to marker file).")]
    public string? DomainNamespace { get; init; }

    /// <summary>
    /// Gets the API documentation UI wired into the host: <c>None</c>, <c>Swagger</c> or <c>Scalar</c> (default).
    /// </summary>
    [CommandOption("--host-ui <TYPE>")]
    [Description("API documentation UI: None, Swagger, or Scalar (default).")]
    public string? HostUi { get; init; }

    /// <summary>
    /// Gets when the documentation UI is enabled: <c>DevelopmentOnly</c> (default) or <c>Always</c>.
    /// </summary>
    [CommandOption("--host-ui-mode <MODE>")]
    [Description("When to enable host UI: DevelopmentOnly (default) or Always.")]
    public string? HostUiMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether an Aspire AppHost project is scaffolded next to the host.
    /// </summary>
    [CommandOption("--aspire")]
    [Description("Create an Aspire AppHost project for orchestration.")]
    [DefaultValue(false)]
    public bool Aspire { get; init; }

    /// <summary>
    /// Gets a value indicating whether a <c>.generation-report.md</c> is written to the output directory.
    /// </summary>
    [CommandOption("--report")]
    [Description("Generate a .generation-report.md file in the output directory.")]
    [DefaultValue(false)]
    public bool GenerateReport { get; init; }

    /// <summary>
    /// Runs the base validation, resolves the domain output path and checks every strategy, version, name and UI option.
    /// </summary>
    /// <returns>A success result, or an error naming the first option that is invalid.</returns>
    public override ValidationResult Validate()
    {
        var baseResult = base.Validate();
        if (!baseResult.Successful)
        {
            return baseResult;
        }

        // Resolve DomainOutputPath if provided
        if (!string.IsNullOrWhiteSpace(DomainOutputPath))
        {
            DomainOutputPath = PathHelper.ResolveRelativePath(DomainOutputPath);
        }

        // Validate sub-folder strategy if provided
        if (!string.IsNullOrWhiteSpace(SubFolderStrategy) &&
            !Enum.TryParse<SubFolderStrategyType>(SubFolderStrategy, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid sub-folder strategy: '{SubFolderStrategy}'. Valid values: None, FirstPathSegment, OpenApiTag.");
        }

        // Validate versioning strategy if provided
        if (!string.IsNullOrWhiteSpace(VersioningStrategy) &&
            !Enum.TryParse<VersioningStrategyType>(VersioningStrategy, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid versioning strategy: '{VersioningStrategy}'. Valid values: {string.Join(", ", Enum.GetNames<VersioningStrategyType>())}.");
        }

        // Validate default API version format if provided
        if (!string.IsNullOrWhiteSpace(DefaultApiVersion) &&
            !Version.TryParse(DefaultApiVersion, out _))
        {
            return ValidationResult.Error($"Invalid API version format: '{DefaultApiVersion}'. Expected format: major.minor (e.g., '1.0').");
        }

        // Validate stub implementation if provided
        if (!string.IsNullOrWhiteSpace(StubImplementation))
        {
            var validStubs = new[] { "throw-not-implemented", "error-501", "default-value" };
            if (!validStubs.Contains(StubImplementation, StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult.Error($"Invalid stub implementation: '{StubImplementation}'. Valid values: throw-not-implemented, error-501, default-value.");
            }
        }

        // Validate project structure if provided
        if (!string.IsNullOrWhiteSpace(ProjectStructure) &&
            !Enum.TryParse<ProjectStructureType>(ProjectStructure, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid project structure: '{ProjectStructure}'. Valid values: SingleProject, TwoProjects, ThreeProjects.");
        }

        // Validate project name overrides don't contain spaces
        if (!string.IsNullOrWhiteSpace(HostProjectName) && HostProjectName.Contains(' ', StringComparison.Ordinal))
        {
            return ValidationResult.Error("Host project name cannot contain spaces.");
        }

        if (!string.IsNullOrWhiteSpace(ContractsProjectName) && ContractsProjectName.Contains(' ', StringComparison.Ordinal))
        {
            return ValidationResult.Error("Contracts project name cannot contain spaces.");
        }

        if (!string.IsNullOrWhiteSpace(DomainProjectName) && DomainProjectName.Contains(' ', StringComparison.Ordinal))
        {
            return ValidationResult.Error("Domain project name cannot contain spaces.");
        }

        // Validate host UI type if provided
        if (!string.IsNullOrWhiteSpace(HostUi) &&
            !Enum.TryParse<HostUiType>(HostUi, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid host UI type: '{HostUi}'. Valid values: None, Swagger, Scalar.");
        }

        // Validate host UI mode if provided
        if (!string.IsNullOrWhiteSpace(HostUiMode) &&
            !Enum.TryParse<HostUiModeType>(HostUiMode, ignoreCase: true, out _))
        {
            return ValidationResult.Error($"Invalid host UI mode: '{HostUiMode}'. Valid values: DevelopmentOnly, Always.");
        }

        return ValidationResult.Success();
    }
}