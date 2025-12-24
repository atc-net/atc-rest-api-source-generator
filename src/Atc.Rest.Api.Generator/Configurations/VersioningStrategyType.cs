namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Defines the strategy for API versioning.
/// Determines how API versions are specified and routed.
/// </summary>
public enum VersioningStrategyType
{
    /// <summary>
    /// No API versioning - endpoints are not versioned.
    /// </summary>
    None = 0,

    /// <summary>
    /// Version specified via query string parameter (e.g., /api/pets?api-version=1.0).
    /// This is the default ASP.NET API Versioning strategy.
    /// </summary>
    QueryString = 1,

    /// <summary>
    /// Version embedded in URL path segment (e.g., /api/v1/pets).
    /// Requires version to always be specified in the URL.
    /// </summary>
    UrlSegment = 2,

    /// <summary>
    /// Version specified via HTTP header (e.g., X-Api-Version: 1.0).
    /// Keeps URLs clean but requires header support from clients.
    /// </summary>
    Header = 3,
}