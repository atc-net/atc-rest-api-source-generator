namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of package reference analysis.
/// </summary>
public sealed class PackageReferenceResult
{
    /// <summary>
    /// Gets or sets the Atc package version found.
    /// </summary>
    public string? AtcVersion { get; set; }

    /// <summary>
    /// Gets or sets the Atc.Rest package version found.
    /// </summary>
    public string? AtcRestVersion { get; set; }

    /// <summary>
    /// Gets or sets the Atc.Rest.MinimalApi package version found.
    /// </summary>
    public string? AtcRestMinimalApiVersion { get; set; }

    /// <summary>
    /// Gets or sets the Atc.Rest.Client package version found.
    /// </summary>
    public string? AtcRestClientVersion { get; set; }

    /// <summary>
    /// Gets or sets all Atc-related packages found with their versions.
    /// </summary>
    public Dictionary<string, string> AtcPackages { get; set; } = [];

    /// <summary>
    /// Gets or sets all other relevant packages found.
    /// </summary>
    public Dictionary<string, string> OtherPackages { get; set; } = [];

    /// <summary>
    /// Gets or sets packages that may need to be removed or replaced.
    /// </summary>
    public List<string> PackagesToRemove { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether Atc packages were found.
    /// </summary>
    public bool HasAtcPackages => AtcPackages.Count > 0;

    /// <summary>
    /// Gets a value indicating whether this appears to be a valid ATC-generated project.
    /// </summary>
    public bool IsAtcGeneratedProject =>
        !string.IsNullOrEmpty(AtcVersion) || !string.IsNullOrEmpty(AtcRestVersion);
}