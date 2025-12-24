namespace Atc.Rest.Api.Generator.Configurations;

/// <summary>
/// Defines the client code generation mode.
/// </summary>
public enum GenerationModeType
{
    /// <summary>
    /// Generate a single typed HTTP client class with methods for each operation.
    /// This is the default mode.
    /// </summary>
    TypedClient = 0,

    /// <summary>
    /// Generate separate endpoint classes for each operation.
    /// Requires Atc.Rest.Client NuGet package.
    /// </summary>
    EndpointPerOperation = 1,
}