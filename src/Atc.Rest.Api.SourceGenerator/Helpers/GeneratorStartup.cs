namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// The steps the server, client and domain generators take before they generate: one marker file read into its
/// configuration, the project's specification found (one base file, its parts, the overlays the marker lists), and
/// the specification read by <see cref="SpecificationLoader"/> - each problem reported on the way, so the three
/// generators start the same way.
/// </summary>
internal static class GeneratorStartup
{
    /// <summary>
    /// The project's one marker file and its configuration; none (and nothing reported) without a marker or a
    /// specification, or when the marker turns generation off.
    /// </summary>
    /// <typeparam name="TConfig">The configuration the marker holds.</typeparam>
    /// <param name="context">The source-production context the problems are reported to.</param>
    /// <param name="yamlFiles">The project's specification files.</param>
    /// <param name="markers">The project's marker files of this generator.</param>
    /// <returns>The marker and its configuration, or <see langword="null"/>.</returns>
    public static (MarkerFileInfo Marker, TConfig Config)? ReadMarker<TConfig>(
        SourceProductionContext context,
        EquatableArray<YamlFileInfo> yamlFiles,
        EquatableArray<MarkerFileInfo> markers)
        where TConfig : BaseConfig, new()
    {
        // The marker file's presence is the trigger, and there is nothing to generate from without a specification
        if (markers.IsEmpty || yamlFiles.IsEmpty)
        {
            return null;
        }

        // One configuration per project: two marker files are a mistake, not a choice
        if (markers.Values.Length > 1)
        {
            DiagnosticHelpers.ReportMultipleMarkerFiles(context, markers.Values.Select(m => m.Path).ToList());
            return null;
        }

        var marker = markers.Values[0];
        TConfig config;
        try
        {
            config = MarkerConfigInspector.Read<TConfig>(marker.Content, out var markerProblems);
            DiagnosticHelpers.ReportUnknownMarkerSettings(context, marker.Path, markerProblems);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A marker that cannot be read is no configuration at all: generating from the defaults would be a
            // different project
            DiagnosticHelpers.ReportMarkerConfigParseError(context, marker.Path, ex, typeof(TConfig));
            return null;
        }

        return config.Generate ? (marker, config) : null;
    }

    /// <summary>
    /// The project's specification: its one base file (a second one is reported), the base file's parts
    /// (<c>{Base}_{Part}.yaml</c>), and the overlays the marker lists (overlay files are no specifications).
    /// </summary>
    /// <param name="context">The source-production context the problems are reported to.</param>
    /// <param name="markerPath">The marker file's path (overlays are listed relative to it).</param>
    /// <param name="listedOverlays">The overlays the marker lists.</param>
    /// <param name="yamlFiles">The project's specification and overlay files.</param>
    /// <returns>The specification's files, or <see langword="null"/>.</returns>
    public static SpecificationFiles? FindSpecification(
        SourceProductionContext context,
        string markerPath,
        IReadOnlyList<string>? listedOverlays,
        EquatableArray<YamlFileInfo> yamlFiles)
    {
        var (specificationFiles, overlayFiles) = OverlayFileHelper.Split(yamlFiles.Values);
        var overlays = OverlayFileHelper.Resolve(context, markerPath, listedOverlays, overlayFiles);
        if (overlays is null || YamlFileHelper.IdentifyBaseFile(specificationFiles) is not { } baseFile)
        {
            return null;
        }

        // A second base specification: the generator reads one per project
        var otherBases = YamlFileHelper.OtherBaseFiles(specificationFiles, baseFile);
        if (otherBases.Count > 0)
        {
            DiagnosticHelpers.ReportMultipleSpecifications(context, [baseFile.Path, .. otherBases.Select(f => f.Path)]);
            return null;
        }

        var baseName = Path.GetFileNameWithoutExtension(baseFile.Path);
        return new SpecificationFiles(
            baseFile,
            [.. specificationFiles.Where(f => YamlFileHelper.IsPartFile(f.Path, baseName))],
            overlays);
    }

    /// <summary>
    /// The specification read: a base file with parts merged (with <paramref name="mergeConfiguration"/>), otherwise
    /// parsed; the overlays applied, validated by the configured strategy and its diagnostics reported.
    /// </summary>
    /// <param name="context">The source-production context the diagnostics are reported to.</param>
    /// <param name="files">The specification's files.</param>
    /// <param name="config">The generator's configuration.</param>
    /// <param name="mergeConfiguration">The merge configuration of a multi-part specification, or <see langword="null"/> for the default.</param>
    /// <param name="parametersMustBind">Whether a parameter the server could not bind is an error (a server).</param>
    /// <param name="reportParsingError">Reports the generator's own parsing-error diagnostic.</param>
    /// <returns>The document, or <see langword="null"/> when it could not be read or has a validation error.</returns>
    public static OpenApiDocument? Load(
        SourceProductionContext context,
        SpecificationFiles files,
        BaseConfig config,
        MultiPartConfiguration? mergeConfiguration,
        bool parametersMustBind,
        Action<SourceProductionContext, string> reportParsingError)
        => files.PartFiles.Count > 0
            ? SpecificationLoader.MergeAndValidate(
                context,
                files.BaseFile,
                files.PartFiles,
                mergeConfiguration,
                config.ValidateSpecificationStrategy,
                files.Overlays,
                parametersMustBind,
                RuleSeverityOverrides.From(config.RuleSeverities, config.SuppressRules))
            : SpecificationLoader.ParseAndValidate(
                context,
                files.BaseFile.Path,
                files.BaseFile.Content,
                config.ValidateSpecificationStrategy,
                reportParsingError,
                files.Overlays,
                parametersMustBind,
                RuleSeverityOverrides.From(config.RuleSeverities, config.SuppressRules));
}