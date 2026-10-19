namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// What the generators read from the build - the properties and item metadata the package's props make visible to the
/// compiler (<c>build/Atc.Rest.Api.SourceGenerator.props</c>).
/// </summary>
internal static class BuildSettings
{
    /// <summary>
    /// The property that says whether the domain generator may write the handler scaffolds, their signatures and
    /// GlobalUsings.cs into the project; the package's targets make it false for a CI build.
    /// </summary>
    public const string ScaffoldHandlersProperty = "build_property.AtcRestApiScaffoldHandlers";

    /// <summary>
    /// The metadata of a sibling project's server marker that the package's targets add to a domain project's
    /// AdditionalFiles.
    /// </summary>
    public const string SiblingMarkerMetadata = "build_metadata.AdditionalFiles.AtcRestApiSiblingMarker";

    /// <summary>
    /// Whether the handler scaffolds may be written into the project: unless AtcRestApiScaffoldHandlers is false.
    /// </summary>
    /// <param name="globalOptions">The build's global options.</param>
    /// <returns>True when the project's files may be written.</returns>
    public static bool WritesScaffolds(AnalyzerConfigOptions globalOptions)
        => globalOptions is null ||
           !globalOptions.TryGetValue(ScaffoldHandlersProperty, out var value) ||
           !string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether an additional file is a sibling project's server marker, added for the domain generator.
    /// </summary>
    /// <param name="fileOptions">The additional file's options.</param>
    /// <returns>True for a sibling project's marker.</returns>
    public static bool IsSiblingMarker(AnalyzerConfigOptions fileOptions)
        => fileOptions is not null &&
           fileOptions.TryGetValue(SiblingMarkerMetadata, out var value) &&
           string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
}