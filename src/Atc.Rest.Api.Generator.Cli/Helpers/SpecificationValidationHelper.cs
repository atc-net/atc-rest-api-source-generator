namespace Atc.Rest.Api.Generator.Cli.Helpers;

/// <summary>
/// Helper for validating OpenAPI specifications and reporting results to the console.
/// </summary>
internal static class SpecificationValidationHelper
{
    /// <summary>
    /// Validates an OpenAPI specification file and writes diagnostic output to the console.
    /// </summary>
    /// <param name="specPath">The path to the specification file.</param>
    /// <param name="strategy">The validation strategy to apply.</param>
    /// <returns>A tuple indicating success, the parsed document, and any diagnostics.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
    public static (bool Success, OpenApiDocument? Document, IReadOnlyList<DiagnosticMessage> Diagnostics) ValidateSpecificationWithStats(
        string specPath,
        ValidateSpecificationStrategy strategy)
    {
        string yamlContent;
        try
        {
            yamlContent = File.ReadAllText(specPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error reading file: {ex.Message}");
            return (false, null, []);
        }

        try
        {
            var (parsedDoc, openApiDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, specPath);

            if (parsedDoc == null)
            {
                AnsiConsole.MarkupLine("[red]✗[/] Failed to parse OpenAPI specification");

                if (openApiDiagnostic?.Errors != null)
                {
                    foreach (var error in openApiDiagnostic.Errors)
                    {
                        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(error.Message)}[/]");
                    }
                }

                return (false, null, []);
            }

            var diagnostics = OpenApiDocumentValidator.Validate(
                strategy,
                parsedDoc,
                openApiDiagnostic?.Errors ?? [],
                specPath);

            var errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (errors.Count > 0)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Validation failed with {errors.Count} error(s):");
                foreach (var error in errors)
                {
                    AnsiConsole.MarkupLine($"  [red][[{error.RuleId}]] {Markup.Escape(error.Message)}[/]");
                }

                return (false, parsedDoc, diagnostics);
            }

            var warnings = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Warning)
                .ToList();

            if (warnings.Count > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]![/] Validation passed with {warnings.Count} warning(s):");
                foreach (var warning in warnings)
                {
                    AnsiConsole.MarkupLine($"  [yellow][[{warning.RuleId}]] {Markup.Escape(warning.Message)}[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[green]✓[/] Validation passed - no issues found");
            }

            return (true, parsedDoc, diagnostics);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error parsing OpenAPI specification: {ex.Message}");
            return (false, null, []);
        }
    }
}