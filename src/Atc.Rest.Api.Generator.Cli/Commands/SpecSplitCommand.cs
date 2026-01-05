namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to split an OpenAPI specification file into multiple part files.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class SpecSplitCommand : Command<SpecSplitCommandSettings>
{
    public override int Execute(
        CommandContext context,
        SpecSplitCommandSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader();

        return AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .Start("Splitting OpenAPI specification...", ctx =>
            {
                try
                {
                    return ExecuteSplit(settings, ctx);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Error during split: {Markup.Escape(ex.Message)}");
                    return 1;
                }
            });
    }

    private static int ExecuteSplit(
        SpecSplitCommandSettings settings,
        StatusContext ctx)
    {
        // Read the specification
        ctx.Status("Reading specification...");
        var specPath = Path.GetFullPath(settings.SpecificationPath);
        var spec = SpecificationService.ReadFromFile(specPath);

        if (spec.Document == null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to parse specification: {Path.GetFileName(specPath)}");
            return 1;
        }

        AnsiConsole.MarkupLine($"[blue]Source file:[/] {Path.GetFileName(specPath)}");
        AnsiConsole.MarkupLine($"[blue]Split strategy:[/] {settings.Strategy}");
        AnsiConsole.MarkupLine($"[blue]Extract common:[/] {(settings.ExtractCommon ? "Yes" : "No")}");
        AnsiConsole.WriteLine();

        // Split the specification
        ctx.Status("Analyzing and splitting specification...");
        var baseName = Path.GetFileNameWithoutExtension(specPath);
        var splitResult = SpecificationService.Split(
            spec.Document,
            baseName,
            settings.GetSplitStrategyEnum(),
            settings.ExtractCommon);

        // Report diagnostics
        var errors = splitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        var warnings = splitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red]Split errors ({errors.Count}):[/]");
            foreach (var error in errors)
            {
                AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(error.Message)}");
            }

            AnsiConsole.WriteLine();
        }

        if (warnings.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Split warnings ({warnings.Count}):[/]");
            foreach (var warning in warnings)
            {
                AnsiConsole.MarkupLine($"  [yellow]•[/] {Markup.Escape(warning.Message)}");
            }

            AnsiConsole.WriteLine();
        }

        if (!splitResult.IsSuccess)
        {
            AnsiConsole.MarkupLine("[red]✗ Split failed.[/]");
            return 1;
        }

        // Display split statistics
        DisplaySplitStatistics(splitResult);

        // Preview or write output
        if (settings.Preview)
        {
            ctx.Status("Generating preview...");
            DisplayPreview(splitResult);
            AnsiConsole.MarkupLine("[green]✓ Preview generated successfully.[/]");
        }
        else
        {
            ctx.Status("Writing output files...");
            WriteOutputFiles(splitResult, settings);
        }

        return 0;
    }

    private static void DisplaySplitStatistics(SplitResult splitResult)
    {
        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("[blue]File[/]");
        table.AddColumn("[blue]Paths[/]");
        table.AddColumn("[blue]Schemas[/]");
        table.AddColumn("[blue]Lines[/]");

        foreach (var file in splitResult.AllFiles)
        {
            var fileLabel = file.IsBaseFile
                ? "[dim]Base[/]"
                : file.IsCommonFile
                    ? "[dim]Common[/]"
                    : file.PartName ?? string.Empty;

            table.AddRow(
                $"{fileLabel}: {file.FileName}",
                file.PathCount.ToString(CultureInfo.InvariantCulture),
                file.SchemaCount.ToString(CultureInfo.InvariantCulture),
                file.EstimatedLines.ToString(CultureInfo.InvariantCulture));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Summary
        AnsiConsole.MarkupLine($"[blue]Total files:[/] {splitResult.AllFiles.Count.ToString(CultureInfo.InvariantCulture)}");
        AnsiConsole.MarkupLine($"[blue]Part files:[/] {splitResult.PartFiles.Count.ToString(CultureInfo.InvariantCulture)}");
        if (splitResult.CommonFile != null)
        {
            AnsiConsole.MarkupLine("[blue]Common file:[/] Yes");
        }

        AnsiConsole.WriteLine();
    }

    private static void DisplayPreview(SplitResult splitResult)
    {
        foreach (var file in splitResult.AllFiles)
        {
            var header = file.IsBaseFile
                ? "[blue]Base File[/]"
                : file.IsCommonFile
                    ? "[blue]Common File[/]"
                    : $"[blue]Part: {file.PartName}[/]";

            AnsiConsole.MarkupLine($"{header} - {file.FileName}");
            AnsiConsole.WriteLine();

            // Show first 20 lines of each file
            var previewLines = file.Content
                .Split('\n')
                .Take(20)
                .ToList();

            foreach (var line in previewLines)
            {
                AnsiConsole.WriteLine(line);
            }

            if (file.EstimatedLines > 20)
            {
                AnsiConsole.MarkupLine($"[dim]... ({file.EstimatedLines - 20} more lines)[/]");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]─────────────────────────────────────────[/]");
            AnsiConsole.WriteLine();
        }
    }

    private static void WriteOutputFiles(
        SplitResult splitResult,
        SpecSplitCommandSettings settings)
    {
        var outputDir = Path.GetFullPath(settings.OutputPath!);

        // Ensure output directory exists
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        foreach (var file in splitResult.AllFiles)
        {
            var outputPath = Path.Combine(outputDir, file.FileName);
            File.WriteAllText(outputPath, file.Content);
            AnsiConsole.MarkupLine($"[green]✓[/] Written: {file.FileName}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓ Split specification written to:[/] {outputDir}");
    }

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]OpenAPI Specification Split Tool[/]");
        AnsiConsole.WriteLine();
    }
}