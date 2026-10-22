namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// The first step every generator takes: a specification file is parsed (or a multi-part set is merged), its overlays
/// are applied, it is read by <see cref="SpecificationPipeline"/> - the same steps as every CLI command - and its
/// diagnostics reported; generation goes on only when no error was found.
/// </summary>
internal static class SpecificationLoader
{
    /// <summary>
    /// Parses and validates one specification file.
    /// </summary>
    /// <param name="context">The source-production context the diagnostics are reported to.</param>
    /// <param name="yamlPath">The file's path.</param>
    /// <param name="yamlContent">The file's content.</param>
    /// <param name="strategy">The validation strategy.</param>
    /// <param name="reportParsingError">Reports the generator's own parsing-error diagnostic.</param>
    /// <param name="overlays">The overlays applied to the file before it is parsed, in order.</param>
    /// <returns>The document, or <see langword="null"/> when it could not be parsed or has a validation error.</returns>
    public static OpenApiDocument? ParseAndValidate(
        SourceProductionContext context,
        string yamlPath,
        string yamlContent,
        ValidateSpecificationStrategy strategy,
        Action<SourceProductionContext, string> reportParsingError,
        IReadOnlyList<OverlayFile>? overlays = null,
        bool parametersMustBind = false,
        RuleSeverityOverrides? severities = null)
    {
        // A superseded IDE run stops before the parse, the most expensive step
        context.CancellationToken.ThrowIfCancellationRequested();

        if (overlays is { Count: > 0 })
        {
            var (effective, problems) = SpecificationOverlays.Apply(yamlContent, overlays);
            OverlayFileHelper.Report(context, problems);
            if (effective is null)
            {
                return null;
            }

            yamlContent = effective;
        }

        var result = SpecificationPipeline.ParseAndValidate(yamlPath, yamlContent, strategy, parametersMustBind);
        if (result.Document is null)
        {
            // That it failed, and why: the parser's own errors
            reportParsingError(context, yamlPath);
            ReportAndContinue(context, result.Diagnostics, severities);
            return null;
        }

        return ReportAndContinue(context, result.Diagnostics, severities) ? result.Document : null;
    }

    /// <summary>
    /// The document as every generator reads it, for a generator that does not validate it: canonical media types, a
    /// status range (2XX) as its first status, and no content on a HEAD's responses.
    /// </summary>
    /// <param name="document">The document, normalized in place.</param>
    /// <returns>The document.</returns>
    public static OpenApiDocument Normalize(OpenApiDocument document)
        => SpecificationPipeline.Normalize(document);

    /// <summary>
    /// The texts of a specification of several files, the base file first (see <see cref="SpecLocations"/>).
    /// </summary>
    /// <param name="baseFile">The base file.</param>
    /// <param name="partFiles">The part files.</param>
    /// <returns>The files' paths and texts.</returns>
    public static IReadOnlyList<(string FilePath, string Text)> SourcesOf(
        YamlFileInfo baseFile,
        IReadOnlyList<YamlFileInfo> partFiles)
        => [(baseFile.Path, baseFile.Content), .. partFiles.Select(p => (p.Path, p.Content))];

    /// <summary>
    /// Merges a base specification with its parts, applies the overlays to the result and validates it like a single
    /// file.
    /// </summary>
    /// <param name="context">The source-production context the diagnostics are reported to.</param>
    /// <param name="baseFile">The base file.</param>
    /// <param name="partFiles">The part files.</param>
    /// <param name="mergeConfiguration">The merge configuration, or <see langword="null"/> for the default.</param>
    /// <param name="strategy">The validation strategy.</param>
    /// <param name="overlays">The overlays applied to the merged document, in order.</param>
    /// <returns>The merged document, or <see langword="null"/> when the merge failed or the result has a validation error.</returns>
    public static OpenApiDocument? MergeAndValidate(
        SourceProductionContext context,
        YamlFileInfo baseFile,
        IReadOnlyList<YamlFileInfo> partFiles,
        MultiPartConfiguration? mergeConfiguration,
        ValidateSpecificationStrategy strategy,
        IReadOnlyList<OverlayFile>? overlays = null,
        bool parametersMustBind = false,
        IEnumerable<string>? suppressRules = null)
    {
        // A superseded IDE run stops before the parse, the most expensive step
        context.CancellationToken.ThrowIfCancellationRequested();

        var severities = RuleSeverityOverrides.From(baseFile.RuleSeverities, suppressRules);
        var document = Merge(context, baseFile, partFiles, mergeConfiguration, overlays, severities);
        if (document is null)
        {
            return null;
        }

        // On the line of the file - the base or a part - that declares the operation, the path or the schema
        var result = SpecificationPipeline.Validate(document, SourcesOf(baseFile, partFiles), strategy, parametersMustBind);
        return ReportAndContinue(context, result.Diagnostics, severities) ? result.Document : null;
    }

    /// <summary>
    /// Merges a base specification with its parts and applies the overlays to the result, without validating it.
    /// </summary>
    /// <param name="context">The source-production context the diagnostics are reported to.</param>
    /// <param name="baseFile">The base file.</param>
    /// <param name="partFiles">The part files.</param>
    /// <param name="mergeConfiguration">The merge configuration, or <see langword="null"/> for the default.</param>
    /// <param name="overlays">The overlays applied to the merged document, in order.</param>
    /// <returns>The merged document, or <see langword="null"/> when the merge or an overlay failed.</returns>
    public static OpenApiDocument? Merge(
        SourceProductionContext context,
        YamlFileInfo baseFile,
        IReadOnlyList<YamlFileInfo> partFiles,
        MultiPartConfiguration? mergeConfiguration,
        IReadOnlyList<OverlayFile>? overlays,
        RuleSeverityOverrides? severities = null)
    {
        var baseSpec = SpecificationService.ReadFromContent(baseFile.Content, baseFile.Path);
        var partSpecs = partFiles
            .Select(p => SpecificationService.ReadFromContent(p.Content, p.Path))
            .ToList();

        var mergeResult = SpecificationService.MergeSpecifications(baseSpec, partSpecs, mergeConfiguration ?? MultiPartConfiguration.Default);
        foreach (var diagnostic in mergeResult.Diagnostics)
        {
            if ((severities ?? RuleSeverityOverrides.None).Apply(diagnostic) is { } reported)
            {
                context.ReportDiagnostic(DiagnosticHelpers.ToRoslynDiagnostic(reported));
            }
        }

        if (!mergeResult.IsSuccess || mergeResult.Document is null)
        {
            return null;
        }

        if (overlays is not { Count: > 0 })
        {
            return mergeResult.Document;
        }

        // The overlays see the whole merged document, so they can change what any part contributed
        var (effective, problems) = SpecificationOverlays.Apply(mergeResult.Document, overlays);
        OverlayFileHelper.Report(context, problems);
        if (effective is null)
        {
            return null;
        }

        var (document, _) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(effective, baseFile.Path);
        return document;
    }

    /// <summary>
    /// Reports every diagnostic; generation goes on unless one is an error.
    /// </summary>
    /// <remarks>
    /// The project's severities (<see cref="RuleSeverityOverrides"/>) change what is reported, not whether generation
    /// goes on: that is decided by the rules' own severities.
    /// </remarks>
    private static bool ReportAndContinue(
        SourceProductionContext context,
        IReadOnlyList<GeneratorDiagnosticMessage> diagnostics,
        RuleSeverityOverrides? severities = null)
    {
        foreach (var diagnostic in diagnostics)
        {
            if ((severities ?? RuleSeverityOverrides.None).Apply(diagnostic) is { } reported)
            {
                context.ReportDiagnostic(DiagnosticHelpers.ToRoslynDiagnostic(reported));
            }
        }

        return !DiagnosticHelpers.HasErrors(diagnostics);
    }
}