namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Holds the path and raw JSON content of a marker file with value-based equality
/// for use in incremental generator pipelines. Config deserialization is deferred
/// to the RegisterSourceOutput callback to avoid equality issues with config classes.
/// </summary>
internal readonly record struct MarkerFileInfo(string Path, string Content) : IEquatable<MarkerFileInfo>;