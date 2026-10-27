namespace Atc.Rest.Api.Generator.Overlays;

/// <summary>
/// An overlay file: where it is and what it holds.
/// </summary>
/// <param name="FilePath">The file's path.</param>
/// <param name="Content">The file's YAML or JSON text.</param>
public sealed record OverlayFile(
    string FilePath,
    string Content);