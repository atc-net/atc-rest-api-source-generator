namespace Atc.Rest.Api.CliGenerator.Commands.Settings;

/// <summary>
/// Settings for the generate server command.
/// </summary>
public sealed class GenerateServerCommandSettings : BaseGenerateCommandSettings
{
    [CommandOption("--sub-folder-strategy <STRATEGY>")]
    [Description("Sub-folder organization strategy: None, FirstPathSegment, or OpenApiTag.")]
    public string? SubFolderStrategy { get; init; }

    [CommandOption("--versioning-strategy <STRATEGY>")]
    [Description("API versioning strategy: None, QueryString, Header, UrlSegment, or Combined.")]
    public string? VersioningStrategy { get; init; }

    [CommandOption("--default-api-version <VERSION>")]
    [Description("Default API version (e.g., '1.0').")]
    public string? DefaultApiVersion { get; init; }

    [CommandOption("--domain-output <PATH>")]
    [Description("Output path for domain handler scaffolds (TwoProjects/TreeProjects mode only).")]
    public string? DomainOutputPath { get; init; }

    [CommandOption("--handler-suffix <SUFFIX>")]
    [Description("Suffix for handler class names (default: 'Handler').")]
    public string? HandlerSuffix { get; init; }

    [CommandOption("--stub-implementation <TYPE>")]
    [Description("Stub implementation type: throw-not-implemented, error-501, or default-value.")]
    public string? StubImplementation { get; init; }

    [CommandOption("--project-structure <TYPE>")]
    [Description("Project structure: SingleProject (1 project), TwoProjects (Host+Contracts, Domain), or TreeProjects (3 projects, default).")]
    public string? ProjectStructure { get; init; }

    [CommandOption("--no-coding-rules")]
    [Description("Skip adding ATC coding rules updater files (added by default).")]
    [DefaultValue(false)]
    public bool NoCodingRules { get; init; }

    [CommandOption("--host-project <NAME>")]
    [Description("Override the host project name (TreeProjects mode only).")]
    public string? HostProjectName { get; init; }

    [CommandOption("--contracts-project <NAME>")]
    [Description("Override the contracts project name.")]
    public string? ContractsProjectName { get; init; }

    [CommandOption("--domain-project <NAME>")]
    [Description("Override the domain project name (TwoProjects/TreeProjects mode only).")]
    public string? DomainProjectName { get; init; }

    [CommandOption("--host-namespace <NAMESPACE>")]
    [Description("Override the host namespace.")]
    public string? HostNamespace { get; init; }

    [CommandOption("--contracts-namespace <NAMESPACE>")]
    [Description("Override the contracts namespace (written to marker file).")]
    public string? ContractsNamespace { get; init; }

    [CommandOption("--domain-namespace <NAMESPACE>")]
    [Description("Override the domain namespace (written to marker file).")]
    public string? DomainNamespace { get; init; }

    [CommandOption("--host-ui <TYPE>")]
    [Description("API documentation UI: None, Swagger, or Scalar (default).")]
    public string? HostUi { get; init; }

    [CommandOption("--host-ui-mode <MODE>")]
    [Description("When to enable host UI: DevelopmentOnly (default) or Always.")]
    public string? HostUiMode { get; init; }

    [CommandOption("--aspire")]
    [Description("Create an Aspire AppHost project for orchestration.")]
    [DefaultValue(false)]
    public bool Aspire { get; init; }

    [CommandOption("--report")]
    [Description("Generate a .generation-report.md file in the output directory.")]
    [DefaultValue(false)]
    public bool GenerateReport { get; init; }

    public override ValidationResult Validate()
    {
        var baseResult = base.Validate();
        if (!baseResult.Successful)
        {
            return baseResult;
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
            return ValidationResult.Error($"Invalid versioning strategy: '{VersioningStrategy}'. Valid values: None, QueryString, Header, UrlSegment, Combined.");
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
            return ValidationResult.Error($"Invalid project structure: '{ProjectStructure}'. Valid values: SingleProject, TwoProjects, TreeProjects.");
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