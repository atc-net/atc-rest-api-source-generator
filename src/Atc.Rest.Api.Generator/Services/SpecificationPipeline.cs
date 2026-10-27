namespace Atc.Rest.Api.Generator.Services;

/// <summary>
/// How a specification is read before anything is generated from it - one order for the build's generators and every
/// CLI command: parsed (or merged from its parts), its media types made canonical, validated by the configured
/// strategy (a server's unbindable parameter an error), and, when no error was found, every status range (<c>2XX</c>)
/// made its first status and a HEAD's responses stripped of content. Overlays are applied by the caller first; the
/// caller reports the diagnostics (the build to Roslyn, the CLI to the console).
/// </summary>
public static class SpecificationPipeline
{
    /// <summary>
    /// Parses and validates one specification file.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <param name="content">The file's content, after the overlays.</param>
    /// <param name="strategy">The validation strategy.</param>
    /// <param name="parametersMustBind">Whether a parameter the server could not bind is an error (a server).</param>
    /// <returns>The document (none when it could not be parsed) and the diagnostics.</returns>
    public static SpecificationPipelineResult ParseAndValidate(
        string path,
        string content,
        ValidateSpecificationStrategy strategy,
        bool parametersMustBind = false)
    {
        var (document, parserDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(content, path);
        if (document is null)
        {
            // That it failed, and why: the parser's own errors
            return new SpecificationPipelineResult(
                Document: null,
                [.. (parserDiagnostic?.Errors ?? []).Select(e => new DiagnosticMessage(RuleIdentifiers.OpenApiCoreError, e.Message, DiagnosticSeverity.Error, path))]);
        }

        // Every extractor looks a body up by its media type: parameters, casing and model-carrying wildcards first
        MediaTypeNormalizer.WithCanonicalMediaTypes(document);

        var diagnostics = Validators.OpenApiDocumentValidator.Validate(
            strategy,
            document,
            parserDiagnostic?.Errors ?? [],
            path,
            content,
            parserDiagnostic?.Warnings);

        return Finish(document, diagnostics, parametersMustBind);
    }

    /// <summary>
    /// Validates a specification merged from its files, each diagnostic on the line of the file - the base or a part -
    /// that declares what it is about.
    /// </summary>
    /// <param name="document">The merged document, after the overlays.</param>
    /// <param name="sources">The files' paths and texts, the base file first.</param>
    /// <param name="strategy">The validation strategy.</param>
    /// <param name="parametersMustBind">Whether a parameter the server could not bind is an error (a server).</param>
    /// <returns>The document and the diagnostics.</returns>
    public static SpecificationPipelineResult Validate(
        OpenApiDocument document,
        IReadOnlyList<(string FilePath, string Text)> sources,
        ValidateSpecificationStrategy strategy,
        bool parametersMustBind = false)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (sources is not { Count: > 0 })
        {
            throw new ArgumentException("The base file is the first source.", nameof(sources));
        }

        MediaTypeNormalizer.WithCanonicalMediaTypes(document);

        var diagnostics = Validators.OpenApiDocumentValidator.Validate(
            strategy,
            document,
            Array.Empty<OpenApiError>(),
            sources[0].FilePath);
        Validators.SpecLocations.Apply(diagnostics, sources);

        return Finish(document, diagnostics, parametersMustBind);
    }

    /// <summary>
    /// The document as every generator reads it, for a caller that does not validate it: canonical media types, a
    /// status range (<c>2XX</c>) as its first status, and no content on a HEAD's responses.
    /// </summary>
    /// <param name="document">The document, normalized in place.</param>
    /// <returns>The document.</returns>
    public static OpenApiDocument Normalize(OpenApiDocument document)
    {
        MediaTypeNormalizer.WithCanonicalMediaTypes(document);
        return HeadResponseNormalizer.WithoutResponseBodies(ResponseRangeNormalizer.WithExplicitStatuses(document));
    }

    /// <summary>
    /// The diagnostics a server reports: a parameter or a form field whose value cannot be bound
    /// (<c>ATC_API_OPR037</c>, a warning to a client, which leaves it out) is an error - the server's code could not
    /// bind it.
    /// </summary>
    /// <param name="diagnostics">The validation diagnostics.</param>
    /// <returns>The diagnostics, with those warnings as errors.</returns>
    public static List<DiagnosticMessage> WithUnbindableParametersAsErrors(
        IEnumerable<DiagnosticMessage> diagnostics)
        => [.. (diagnostics ?? []).Select(d => d.RuleId == RuleIdentifiers.ParameterValueNotBindable && d.Severity == DiagnosticSeverity.Warning
            ? d with { Severity = DiagnosticSeverity.Error }
            : d)];

    private static SpecificationPipelineResult Finish(
        OpenApiDocument document,
        List<DiagnosticMessage> diagnostics,
        bool parametersMustBind)
    {
        if (parametersMustBind)
        {
            diagnostics = WithUnbindableParametersAsErrors(diagnostics);
        }

        // A status range (2XX) is its first status for every generator, and a HEAD's responses have no content (the
        // validator has warned about both)
        return diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)
            ? new SpecificationPipelineResult(document, diagnostics)
            : new SpecificationPipelineResult(HeadResponseNormalizer.WithoutResponseBodies(ResponseRangeNormalizer.WithExplicitStatuses(document)), diagnostics);
    }
}