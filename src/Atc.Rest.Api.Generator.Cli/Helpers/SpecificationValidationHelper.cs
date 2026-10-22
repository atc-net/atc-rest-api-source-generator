namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Helper for validating OpenAPI specifications and reporting results to the console.
/// </summary>
internal static class SpecificationValidationHelper
{
    /// <summary>
    /// Reads a specification the way the build does (<see cref="SpecificationPipeline"/>): its parts merged, the
    /// overlays applied to the whole, then validated, and writes what it found to the console.
    /// </summary>
    /// <param name="specPath">The path to the specification file (the base file of a multi-part specification).</param>
    /// <param name="strategy">The validation strategy to apply.</param>
    /// <param name="overlays">The <c>--overlay</c> files applied to the specification first.</param>
    /// <param name="parametersMustBind">Whether a parameter the server could not bind is an error (a server).</param>
    /// <returns>A tuple indicating success, the document, and any diagnostics.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
    public static (bool Success, OpenApiDocument? Document, IReadOnlyList<DiagnosticMessage> Diagnostics) ValidateSpecificationWithStats(
        string specPath,
        ValidateSpecificationStrategy strategy,
        IReadOnlyList<string>? overlays = null,
        bool parametersMustBind = false)
    {
        string yamlContent;
        try
        {
            yamlContent = File.ReadAllText(specPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error reading file: {ex.Message}");
            return (false, null, []);
        }

        try
        {
            // A file that is no OpenAPI specification is reported with the parser's errors
            var (parsedDoc, openApiDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, specPath);
            if (parsedDoc is null)
            {
                WriteParseErrors((openApiDiagnostic?.Errors ?? []).Select(e => e.Message));
                return (false, null, []);
            }

            var result = Read(specPath, yamlContent, overlays ?? [], strategy, parametersMustBind);
            if (result is null)
            {
                return (false, null, []);
            }

            if (result.Document is null)
            {
                WriteParseErrors(result.Diagnostics.Select(d => d.Message));
                return (false, null, []);
            }

            return (WriteResults(result.Diagnostics), result.Document, result.Diagnostics);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error parsing OpenAPI specification: {ex.Message}");
            return (false, null, []);
        }
    }

    // The specification with its parts ({Base}_{Part}.yaml, or an x-multipart list) merged and the overlays applied to
    // the whole, as in the build; null after an error it printed
    private static SpecificationPipelineResult? Read(
        string specPath,
        string yamlContent,
        IReadOnlyList<string> overlays,
        ValidateSpecificationStrategy strategy,
        bool parametersMustBind)
    {
        var (merge, merged) = SpecificationService.ReadAndMergeResolved(specPath);
        if (merged?.Document is null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Failed to merge the multi-part specification:");
            foreach (var diagnostic in merge.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                var location = diagnostic.FilePath is null ? string.Empty : " (" + Path.GetFileName(diagnostic.FilePath) + ")";
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red][[{diagnostic.RuleId}]] {diagnostic.Message + location}[/]");
            }

            return null;
        }

        if (!merge.IsMultiPart)
        {
            var effective = OverlayCliHelper.Apply(yamlContent, overlays);
            return effective is null
                ? null
                : SpecificationPipeline.ParseAndValidate(specPath, effective, strategy, parametersMustBind);
        }

        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Merged {merge.PartFiles.Count} part file(s) into the specification");
        var document = merged.Document;
        if (overlays.Count > 0)
        {
            if (OverlayCliHelper.Apply(merged.Content, overlays) is not { } overlaid)
            {
                return null;
            }

            if (OpenApiDocumentHelper.TryParseYamlWithDiagnostic(overlaid, specPath).Document is not { } overlaidDocument)
            {
                AnsiConsole.MarkupLine("[red]✗[/] The overlays make a document that is no OpenAPI specification");
                return null;
            }

            document = overlaidDocument;
        }

        // Each diagnostic on the line of the file - the base or a part - that declares what it is about
        return SpecificationPipeline.Validate(
            document,
            [(specPath, yamlContent), .. merge.PartFiles.Select(p => (p.FilePath, p.Content))],
            strategy,
            parametersMustBind);
    }

    private static void WriteParseErrors(IEnumerable<string> messages)
    {
        AnsiConsole.MarkupLine("[red]✗[/] Failed to parse OpenAPI specification");
        foreach (var message in messages)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red]{message}[/]");
        }
    }

    // The errors, or the warnings; true when there is no error
    private static bool WriteResults(
        IReadOnlyList<DiagnosticMessage> diagnostics)
    {
        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Validation failed with {errors.Count} error(s):");
            foreach (var error in errors)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red][[{error.RuleId}]] {error.Message}[/]");
            }

            return false;
        }

        var warnings = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        if (warnings.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] Validation passed with {warnings.Count} warning(s):");
            foreach (var warning in warnings)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [yellow][[{warning.RuleId}]] {warning.Message}[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[green]✓[/] Validation passed - no issues found");
        }

        return true;
    }
}