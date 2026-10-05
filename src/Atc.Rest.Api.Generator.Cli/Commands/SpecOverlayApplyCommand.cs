namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Applies OpenAPI Overlays to a specification and writes the result: the specification the build generates from
/// when the marker lists the same overlays. Useful to see what an overlay does, and to debug one.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class SpecOverlayApplyCommand : Command<SpecOverlayApplyCommandSettings>
{
    /// <summary>
    /// Merges a multi-part specification, applies the overlays in order and writes the effective specification.
    /// </summary>
    /// <param name="context">The Spectre.Console command context.</param>
    /// <param name="settings">The parsed command-line settings.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>0 when the overlays applied and the result was written; otherwise 1.</returns>
    protected override int Execute(
        CommandContext context,
        SpecOverlayApplyCommandSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        AnsiConsole.MarkupLine($"[blue]Specification:[/] {Markup.Escape(settings.SpecificationPath)}");
        foreach (var overlay in settings.Overlays)
        {
            AnsiConsole.MarkupLine($"[blue]Overlay:[/] {Markup.Escape(OverlayCliHelper.FullPath(overlay))}");
        }

        try
        {
            // A multi-part specification is merged first, as the build does: the overlays see the whole document
            var (merge, merged) = SpecificationService.ReadAndMergeResolved(settings.SpecificationPath);
            var content = merge.IsMultiPart && merged is not null
                ? merged.Content
                : File.ReadAllText(settings.SpecificationPath);

            var effective = OverlayCliHelper.Apply(content, settings.Overlays);
            if (effective is null)
            {
                return 1;
            }

            var (document, diagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(effective, settings.SpecificationPath);
            if (document is null)
            {
                AnsiConsole.MarkupLine("[red]✗[/] The overlays make a document that is no OpenAPI specification:");
                foreach (var error in diagnostic?.Errors ?? [])
                {
                    AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(error.Message)}");
                }

                return 1;
            }

            var output = settings.OutputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? SpecificationService.SerializeToJson(document)
                : SpecificationService.SerializeToYaml(document);

            var directory = Path.GetDirectoryName(settings.OutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(settings.OutputPath, output);
            AnsiConsole.MarkupLine($"[green]✓[/] Written: {Markup.Escape(settings.OutputPath)}");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error: {Markup.Escape(ex.Message)}");
            return 1;
        }
    }
}