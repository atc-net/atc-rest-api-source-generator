namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Defines how many typed HTTP client classes are generated for a specification.
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "OK - 'Single' is the documented marker-file value for clientGranularity.")]
public enum ClientGranularityType
{
    /// <summary>
    /// Generate one client per API area (first path segment).
    /// This is the default and matches the historical behaviour.
    /// </summary>
    PerArea = 0,

    /// <summary>
    /// Generate a single client covering all operations across all path segments.
    /// The client is emitted into {root}.Generated and models into {root}.Generated.Models.
    /// </summary>
    Single = 1,
}