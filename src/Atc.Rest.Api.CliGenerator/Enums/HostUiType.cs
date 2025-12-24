namespace Atc.Rest.Api.CliGenerator.Enums;

/// <summary>
/// API documentation UI provider type.
/// </summary>
public enum HostUiType
{
    /// <summary>
    /// No API documentation UI.
    /// </summary>
    None,

    /// <summary>
    /// Swagger UI (Swashbuckle).
    /// </summary>
    Swagger,

    /// <summary>
    /// Scalar API Reference (default).
    /// </summary>
    Scalar,
}