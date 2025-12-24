namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Specifies how the generator handles Atc.Rest.MinimalApi package integration.
/// </summary>
public enum MinimalApiPackageMode
{
    /// <summary>
    /// Auto-detect package reference. If referenced, use package interface.
    /// </summary>
    Auto,

    /// <summary>
    /// Force use of package interface. Report error if package not referenced.
    /// </summary>
    Enabled,

    /// <summary>
    /// Always generate IEndpointDefinition interface (legacy behavior).
    /// </summary>
    Disabled,
}