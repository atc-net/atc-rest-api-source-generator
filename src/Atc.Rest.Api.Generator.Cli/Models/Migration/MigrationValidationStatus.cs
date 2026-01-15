namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Overall validation status for migration.
/// </summary>
public enum MigrationValidationStatus
{
    /// <summary>
    /// Status has not been determined yet.
    /// </summary>
    Unknown,

    /// <summary>
    /// Project is ready for migration without any changes.
    /// </summary>
    Ready,

    /// <summary>
    /// Project requires .NET/C# upgrade before migration.
    /// </summary>
    RequiresUpgrade,

    /// <summary>
    /// Project cannot be migrated due to blocking issues.
    /// </summary>
    Blocked,
}
