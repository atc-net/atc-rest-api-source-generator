namespace Atc.Rest.Api.Generator.Cli.Enums;

/// <summary>
/// Controls when the API documentation UI is enabled.
/// </summary>
public enum HostUiModeType
{
    /// <summary>
    /// Enable UI only in development environment (default).
    /// </summary>
    DevelopmentOnly,

    /// <summary>
    /// Enable UI in all environments including production.
    /// </summary>
    Always,
}