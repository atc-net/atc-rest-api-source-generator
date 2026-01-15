namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Represents the complete validation report for a migration.
/// </summary>
public sealed class MigrationValidationReport
{
    /// <summary>
    /// Gets or sets the solution path that was validated.
    /// </summary>
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the specification path that was validated.
    /// </summary>
    public string SpecificationPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when validation was performed.
    /// </summary>
    public DateTime ValidationTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the project structure validation result.
    /// </summary>
    public ProjectStructureResult ProjectStructure { get; set; } = new();

    /// <summary>
    /// Gets or sets the specification validation result.
    /// </summary>
    public SpecificationResult Specification { get; set; } = new();

    /// <summary>
    /// Gets or sets the generator options result.
    /// </summary>
    public GeneratorOptionsResult GeneratorOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the package reference analysis result.
    /// </summary>
    public PackageReferenceResult PackageReferences { get; set; } = new();

    /// <summary>
    /// Gets or sets the generated code analysis result.
    /// </summary>
    public GeneratedCodeResult GeneratedCode { get; set; } = new();

    /// <summary>
    /// Gets or sets the handler analysis result.
    /// </summary>
    public HandlerAnalysisResult Handlers { get; set; } = new();

    /// <summary>
    /// Gets or sets the target framework validation result.
    /// </summary>
    public TargetFrameworkResult TargetFramework { get; set; } = new();

    /// <summary>
    /// Gets or sets the overall validation status.
    /// </summary>
    public MigrationValidationStatus Status { get; set; } = MigrationValidationStatus.Unknown;

    /// <summary>
    /// Gets or sets whether .NET/C# upgrade is required.
    /// </summary>
    public bool RequiresUpgrade { get; set; }

    /// <summary>
    /// Gets or sets blocking issues that prevent migration.
    /// </summary>
    public List<string> BlockingIssues { get; set; } = [];

    /// <summary>
    /// Gets or sets warnings that don't block migration but should be addressed.
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Gets the detected project name from the solution/projects.
    /// </summary>
    public string? DetectedProjectName { get; set; }

    /// <summary>
    /// Gets the detected namespace root.
    /// </summary>
    public string? DetectedNamespace { get; set; }

    /// <summary>
    /// Gets a value indicating whether migration can proceed (with or without upgrade prompt).
    /// </summary>
    public bool CanMigrate
        => Status is MigrationValidationStatus.Ready or MigrationValidationStatus.RequiresUpgrade;
}