namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// The files of a project's specification.
/// </summary>
/// <param name="BaseFile">The base file.</param>
/// <param name="PartFiles">The base file's parts (<c>{Base}_{Part}.yaml</c>), none for a single file.</param>
/// <param name="Overlays">The overlays the marker lists, in order.</param>
internal sealed record SpecificationFiles(
    YamlFileInfo BaseFile,
    IReadOnlyList<YamlFileInfo> PartFiles,
    IReadOnlyList<OverlayFile> Overlays);