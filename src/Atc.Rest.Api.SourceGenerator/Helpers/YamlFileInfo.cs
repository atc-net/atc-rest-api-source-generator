namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// Holds the path and content of an OpenAPI YAML file with value-based equality
/// for use in incremental generator pipelines.
/// </summary>
internal readonly record struct YamlFileInfo(string Path, string Content) : IEquatable<YamlFileInfo>;