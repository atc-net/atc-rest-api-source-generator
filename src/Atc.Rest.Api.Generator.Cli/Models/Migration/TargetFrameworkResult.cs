namespace Atc.Rest.Api.Generator.Cli.Models.Migration;

/// <summary>
/// Result of target framework and language version validation.
/// </summary>
public sealed class TargetFrameworkResult
{
    /// <summary>
    /// Gets or sets the detected target framework (e.g., "net9.0", "net8.0").
    /// </summary>
    public string? CurrentTargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the detected C# language version (e.g., "13", "12", "14").
    /// </summary>
    public string? CurrentLangVersion { get; set; }

    /// <summary>
    /// Gets or sets where the target framework was found (Directory.Build.props or individual .csproj).
    /// </summary>
    public string? TargetFrameworkSource { get; set; }

    /// <summary>
    /// Gets or sets where the language version was found.
    /// </summary>
    public string? LangVersionSource { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the target framework is compatible (>= net8.0).
    /// </summary>
    public bool IsTargetFrameworkCompatible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the language version is compatible (>= 12).
    /// </summary>
    public bool IsLangVersionCompatible { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an upgrade to .NET 10 is required.
    /// </summary>
    public bool RequiresTargetFrameworkUpgrade { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an upgrade to C# 14 is required.
    /// </summary>
    public bool RequiresLangVersionUpgrade { get; set; }

    /// <summary>
    /// Gets the required target framework for migration.
    /// </summary>
    public static string RequiredTargetFramework => "net10.0";

    /// <summary>
    /// Gets the required language version for migration.
    /// </summary>
    public static string RequiredLangVersion => "14";

    /// <summary>
    /// Gets a value indicating whether any upgrade is required.
    /// </summary>
    public bool RequiresAnyUpgrade => RequiresTargetFrameworkUpgrade || RequiresLangVersionUpgrade;

    /// <summary>
    /// Gets a value indicating whether the current configuration is fully compatible (no upgrades needed).
    /// </summary>
    public bool IsFullyCompatible =>
        IsTargetFrameworkCompatible && IsLangVersionCompatible && !RequiresAnyUpgrade;

    /// <summary>
    /// Gets a value indicating whether migration is blocked due to incompatible versions.
    /// </summary>
    public bool IsBlocked => !IsTargetFrameworkCompatible || !IsLangVersionCompatible;

    /// <summary>
    /// Gets the numeric version of the target framework (e.g., 9.0 for net9.0).
    /// </summary>
    public decimal? TargetFrameworkVersion
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentTargetFramework))
            {
                return null;
            }

            var versionString = CurrentTargetFramework
                .Replace("net", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("coreapp", string.Empty, StringComparison.OrdinalIgnoreCase);

            return decimal.TryParse(versionString, NumberStyles.Number, CultureInfo.InvariantCulture, out var version)
                ? version
                : null;
        }
    }

    /// <summary>
    /// Gets the numeric version of the language version.
    /// </summary>
    public int? LangVersionNumber
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentLangVersion))
            {
                return null;
            }

            // Handle versions like "13.0", "13", "preview", "latest"
            var cleanVersion = CurrentLangVersion
                .Replace(".0", string.Empty)
                .Replace("preview", "99", StringComparison.OrdinalIgnoreCase)
                .Replace("latest", "99", StringComparison.OrdinalIgnoreCase);

            return int.TryParse(cleanVersion, out var version) ? version : null;
        }
    }
}
