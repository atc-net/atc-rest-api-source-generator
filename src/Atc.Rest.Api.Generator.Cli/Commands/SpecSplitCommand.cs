namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to split an OpenAPI specification file into multiple part files.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class SpecSplitCommand : Command<SpecSplitCommandSettings>
{
    /// <summary>
    /// Splits the specification into a base file and part files by the chosen strategy, previewing or writing them.
    /// </summary>
    /// <param name="context">The Spectre.Console command context.</param>
    /// <param name="settings">The parsed command-line settings.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>0 when the split succeeded; otherwise 1.</returns>
    public override int Execute(
        CommandContext context,
        SpecSplitCommandSettings settings,
        CancellationToken cancellationToken)
    {
        // A command that fails half-way leaves the project as it found it: what it wrote is put back
        using var transaction = FileTransaction.Begin();
        var result = ExecuteCore(context, settings, cancellationToken);
        if (result == 0)
        {
            transaction.Commit();
        }
        else if (transaction.HasChanges)
        {
            AnsiConsole.MarkupLine("[yellow]The files written before the failure were put back.[/]");
        }

        return result;
    }

    private int ExecuteCore(
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
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error during split: {ex.Message}");
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

        if (spec.Document is null)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Failed to parse specification: {Path.GetFileName(specPath)}");
            return 1;
        }

        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Source file:[/] {Path.GetFileName(specPath)}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Split strategy:[/] {settings.Strategy}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Extract common:[/] {(settings.ShouldExtractCommon ? "Yes" : "No")}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Min operations per part:[/] {settings.MinOperations.ToString(CultureInfo.InvariantCulture)}");
        AnsiConsole.WriteLine();

        // Split the specification
        ctx.Status("Analyzing and splitting specification...");
        var baseName = Path.GetFileNameWithoutExtension(specPath);
        var splitResult = SpecificationService.Split(
            spec.Document,
            baseName,
            settings.GetSplitStrategyEnum(),
            settings.ShouldExtractCommon,
            settings.MinOperations);

        // Report diagnostics
        var errors = splitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        var warnings = splitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]Split errors ({errors.Count}):[/]");
            foreach (var error in errors)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red]•[/] {error.Message}");
            }

            AnsiConsole.WriteLine();
        }

        if (warnings.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Split warnings ({warnings.Count}):[/]");
            foreach (var warning in warnings)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [yellow]•[/] {warning.Message}");
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
                    : Markup.Escape(file.PartName ?? string.Empty);

            table.AddRow(
                $"{fileLabel}: {Markup.Escape(file.FileName)}",
                file.PathCount.ToString(CultureInfo.InvariantCulture),
                file.SchemaCount.ToString(CultureInfo.InvariantCulture),
                file.EstimatedLines.ToString(CultureInfo.InvariantCulture));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Summary
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Total files:[/] {splitResult.AllFiles.Count.ToString(CultureInfo.InvariantCulture)}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Part files:[/] {splitResult.PartFiles.Count.ToString(CultureInfo.InvariantCulture)}");
        if (splitResult.CommonFile is not null)
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
                ? "Base File"
                : file.IsCommonFile
                    ? "Common File"
                    : $"Part: {file.PartName}";

            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]{header}[/] - {file.FileName}");
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
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]... ({file.EstimatedLines - 20} more lines)[/]");
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
            TrackedFile.CreateDirectory(outputDir);
        }

        foreach (var file in splitResult.AllFiles)
        {
            var outputPath = Path.Combine(outputDir, file.FileName);
            TrackedFile.WriteAllText(outputPath, file.Content);
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Written: {file.FileName}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓ Split specification written to:[/] {outputDir}");
    }

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]OpenAPI Specification Split Tool[/]");
        AnsiConsole.WriteLine();
    }
}