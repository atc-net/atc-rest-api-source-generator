namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Summary of migration changes.
/// </summary>
public sealed class MigrationSummary
{
    /// <summary>
    /// Gets or sets the specification path written to the marker files.
    /// </summary>
    public string? SpecificationPath { get; set; }

    /// <summary>
    /// Gets the files the migration created, such as the marker files.
    /// </summary>
    public List<string> CreatedFiles { get; } = [];

    /// <summary>
    /// Gets the old generated files that were deleted.
    /// </summary>
    public List<string> DeletedFiles { get; } = [];

    /// <summary>
    /// Gets the old generated folders that were deleted.
    /// </summary>
    public List<string> DeletedFolders { get; } = [];

    /// <summary>
    /// Gets the hand-written files found inside generated folders and kept.
    /// </summary>
    public List<string> PreservedFiles { get; } = [];

    /// <summary>
    /// Gets the packages the source generator makes obsolete, each with the dependencies removing it would drop.
    /// </summary>
    public List<string> ObsoletePackages { get; } = [];

    /// <summary>
    /// Gets the existing files the migration changed, such as project and solution files.
    /// </summary>
    public List<string> ModifiedFiles { get; } = [];

    /// <summary>
    /// Gets the project renames as <c>old → new</c> entries.
    /// </summary>
    public List<string> RenamedProjects { get; } = [];

    /// <summary>
    /// Gets the project references rewritten to the new project names.
    /// </summary>
    public List<string> UpdatedReferences { get; } = [];

    /// <summary>
    /// Gets the GlobalUsings.cs changes in the domain and test projects, prefixed <c>+</c> for added and <c>-</c> for removed namespaces.
    /// </summary>
    public List<string> UpdatedGlobalUsings { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the existing atc-coding-rules-updater.json was moved to the DotNet10 target.
    /// </summary>
    public bool AtcCodingRulesUpdated { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an atc-coding-rules-updater.json was created.
    /// </summary>
    public bool AtcCodingRulesCreated { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the dry run would prompt to set up ATC coding rules.
    /// </summary>
    public bool AtcCodingRulesWillPrompt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Directory.Build.props was upgraded.
    /// </summary>
    public bool DirectoryBuildPropsUpgraded { get; set; }

    /// <summary>
    /// Gets or sets the target framework Directory.Build.props was upgraded to.
    /// </summary>
    public string? UpgradedTargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the language version Directory.Build.props was upgraded to.
    /// </summary>
    public string? UpgradedLangVersion { get; set; }

    /// <summary>
    /// Gets the domain code files whose namespace references were rewritten.
    /// </summary>
    public List<DomainFileUpdate> DomainCodeUpdates { get; } = [];

    /// <summary>
    /// Gets or sets the number of namespace replacements across all domain code files.
    /// </summary>
    public int DomainCodeTotalReplacements { get; set; }

    /// <summary>
    /// Gets the GlobalUsings.cs and Program.cs changes in the host project, prefixed <c>+</c> for added and <c>-</c> for removed lines.
    /// </summary>
    public List<string> HostProjectUpdates { get; } = [];

    /// <summary>
    /// Gets the handler parameter renames applied to the domain code.
    /// </summary>
    public List<string> ParameterMigrations { get; } = [];
}