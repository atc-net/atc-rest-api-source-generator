namespace Atc.Rest.Api.SourceGenerator.Helpers;

/// <summary>
/// The first step every generator takes: a specification file is parsed (or a multi-part set is merged), validated
/// by the configured strategy, and its diagnostics reported - generation goes on only when no error was found. The
/// server, client and domain generators share it, so a specification is read the same way by all three.
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
    /// <returns>The document, or <see langword="null"/> when it could not be parsed or has a validation error.</returns>
    public static OpenApiDocument? ParseAndValidate(
        SourceProductionContext context,
        string yamlPath,
        string yamlContent,
        ValidateSpecificationStrategy strategy,
        Action<SourceProductionContext, string> reportParsingError)
    {
        // A superseded IDE run stops before the parse, the most expensive step
        context.CancellationToken.ThrowIfCancellationRequested();

        var (openApiDoc, openApiDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, yamlPath);
        if (openApiDoc is null)
        {
            reportParsingError(context, yamlPath);
            return null;
        }

        var validationDiagnostics = OpenApiDocumentValidator.Validate(
            strategy,
            openApiDoc,
            openApiDiagnostic?.Errors ?? Array.Empty<OpenApiError>(),
            yamlPath,
            yamlContent);

        return ReportAndContinue(context, validationDiagnostics) ? openApiDoc : null;
    }

    /// <summary>
    /// Merges a base specification with its parts and validates the result like a single file.
    /// </summary>
    /// <param name="context">The source-production context the diagnostics are reported to.</param>
    /// <param name="baseFile">The base file.</param>
    /// <param name="partFiles">The part files.</param>
    /// <param name="mergeConfiguration">The merge configuration, or <see langword="null"/> for the default.</param>
    /// <param name="strategy">The validation strategy.</param>
    /// <returns>The merged document, or <see langword="null"/> when the merge failed or the result has a validation error.</returns>
    public static OpenApiDocument? MergeAndValidate(
        SourceProductionContext context,
        YamlFileInfo baseFile,
        IReadOnlyList<YamlFileInfo> partFiles,
        MultiPartConfiguration? mergeConfiguration,
        ValidateSpecificationStrategy strategy)
    {
        // A superseded IDE run stops before the parse, the most expensive step
        context.CancellationToken.ThrowIfCancellationRequested();

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

        var validationDiagnostics = OpenApiDocumentValidator.Validate(
            strategy,
            mergeResult.Document,
            Array.Empty<OpenApiError>(),
            baseFile.Path);

        return ReportAndContinue(context, validationDiagnostics) ? mergeResult.Document : null;
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