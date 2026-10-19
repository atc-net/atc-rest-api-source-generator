namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// How the domain generator scaffolds the handlers of one run.
/// </summary>
/// <param name="ServerMarkers">The server markers among the project's AdditionalFiles (its own, and a sibling project's).</param>
/// <param name="WritesFiles">Whether the scaffolds, their signatures and GlobalUsings.cs may be written into the project.</param>
internal sealed record ScaffoldSettings(
    IReadOnlyList<MarkerFileInfo> ServerMarkers,
    bool WritesFiles);