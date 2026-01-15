namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Summary of migration changes.
/// </summary>
public sealed class MigrationSummary
{
    public string? SpecificationPath { get; set; }

    public List<string> CreatedFiles { get; } = [];

    public List<string> DeletedFiles { get; } = [];

    public List<string> DeletedFolders { get; } = [];

    public List<string> ModifiedFiles { get; } = [];

    public List<string> RenamedProjects { get; } = [];

    public List<string> UpdatedReferences { get; } = [];

    public List<string> UpdatedGlobalUsings { get; } = [];

    public bool AtcCodingRulesUpdated { get; set; }

    public bool AtcCodingRulesCreated { get; set; }

    public bool AtcCodingRulesWillPrompt { get; set; }

    public bool DirectoryBuildPropsUpgraded { get; set; }

    public string? UpgradedTargetFramework { get; set; }

    public string? UpgradedLangVersion { get; set; }

    public List<DomainFileUpdate> DomainCodeUpdates { get; } = [];

    public int DomainCodeTotalReplacements { get; set; }

    public List<string> HostProjectUpdates { get; } = [];
}