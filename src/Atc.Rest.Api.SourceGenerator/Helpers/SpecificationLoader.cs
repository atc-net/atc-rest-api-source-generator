namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// The first step every generator takes: a specification file is parsed (or a multi-part set is merged), its overlays
/// are applied, it is validated by the configured strategy, and its diagnostics reported - generation goes on only
/// when no error was found. The server, client and domain generators share it, so a specification is read the same
/// way by all three.
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
        IReadOnlyList<OverlayFile>? overlays = null)
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

        var (openApiDoc, openApiDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, yamlPath);
        if (openApiDoc is null)
        {
            // That it failed, and why: the parser's own errors
            reportParsingError(context, yamlPath);
            ReportAndContinue(
                context,
                [.. (openApiDiagnostic?.Errors ?? []).Select(e => new GeneratorDiagnosticMessage(RuleIdentifiers.OpenApiCoreError, e.Message, GeneratorDiagnosticSeverity.Error, yamlPath))]);
            return null;
        }

        // Every extractor looks a body up by its media type: parameters, casing and model-carrying wildcards first
        MediaTypeNormalizer.WithCanonicalMediaTypes(openApiDoc);

        var validationDiagnostics = OpenApiDocumentValidator.Validate(
            strategy,
            openApiDoc,
            openApiDiagnostic?.Errors ?? Array.Empty<OpenApiError>(),
            yamlPath,
            yamlContent);

        // A status range (2XX) is its first status for every generator (the validator has warned about it)
        return ReportAndContinue(context, validationDiagnostics) ? ResponseRangeNormalizer.WithExplicitStatuses(openApiDoc) : null;
    }

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
        IReadOnlyList<OverlayFile>? overlays = null)
    {
        // A superseded IDE run stops before the parse, the most expensive step
        context.CancellationToken.ThrowIfCancellationRequested();

        var document = Merge(context, baseFile, partFiles, mergeConfiguration, overlays);
        if (document is null)
        {
            return null;
        }

        MediaTypeNormalizer.WithCanonicalMediaTypes(document);

        var validationDiagnostics = OpenApiDocumentValidator.Validate(
            strategy,
            document,
            Array.Empty<OpenApiError>(),
            baseFile.Path);

        return ReportAndContinue(context, validationDiagnostics) ? ResponseRangeNormalizer.WithExplicitStatuses(document) : null;
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
        IReadOnlyList<OverlayFile>? overlays)
    {
        var baseSpec = SpecificationService.ReadFromContent(baseFile.Content, baseFile.Path);
        var partSpecs = partFiles
            .Select(p => SpecificationService.ReadFromContent(p.Content, p.Path))
            .ToList();

        var mergeResult = SpecificationService.MergeSpecifications(baseSpec, partSpecs, mergeConfiguration ?? MultiPartConfiguration.Default);
        foreach (var diagnostic in mergeResult.Diagnostics)
        {
            context.ReportDiagnostic(DiagnosticHelpers.ToRoslynDiagnostic(diagnostic));
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
    private static bool ReportAndContinue(
        SourceProductionContext context,
        IReadOnlyList<GeneratorDiagnosticMessage> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            context.ReportDiagnostic(DiagnosticHelpers.ToRoslynDiagnostic(diagnostic));
        }

        return !DiagnosticHelpers.HasErrors(diagnostics);
    }
}