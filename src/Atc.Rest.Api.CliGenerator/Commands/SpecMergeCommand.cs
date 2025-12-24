namespace Atc.Rest.Api.CliGenerator.Commands;

/// <summary>
/// Command to merge multiple OpenAPI specification files into one.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class SpecMergeCommand : Command<SpecMergeCommandSettings>
{
    public override int Execute(
        CommandContext context,
        SpecMergeCommandSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader();

        return AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .Start("Merging OpenAPI specifications...", ctx =>
            {
                try
                {
                    return ExecuteMerge(settings, ctx);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Error during merge: {Markup.Escape(ex.Message)}");
                    return 1;
                }
            });
    }

    private static int ExecuteMerge(
        SpecMergeCommandSettings settings,
        StatusContext ctx)
    {
        // Determine files to merge
        var filesToMerge = GetFilesToMerge(settings);
        if (filesToMerge.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No files found to merge.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[blue]Files to merge:[/]");
        foreach (var file in filesToMerge)
        {
            AnsiConsole.MarkupLine($"  [dim]•[/] {Path.GetFileName(file)}");
        }

        AnsiConsole.WriteLine();

        // Read base file
        ctx.Status("Reading base specification...");
        var baseFile = filesToMerge[0];
        var baseSpec = SpecificationService.ReadFromFile(baseFile);

        if (baseSpec.Document == null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to parse base specification: {Path.GetFileName(baseFile)}");
            return 1;
        }

        // Read part files
        ctx.Status("Reading part files...");
        var partSpecs = new List<SpecificationFile>();
        foreach (var partFile in filesToMerge.Skip(1))
        {
            var spec = SpecificationService.ReadFromFile(partFile);
            if (spec.Document == null)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to parse part file: {Path.GetFileName(partFile)}");
                continue;
            }

            partSpecs.Add(spec);
        }

        // Create merge configuration
        var mergeConfig = new MultiPartConfiguration
        {
            PathsMergeStrategy = settings.GetMergeStrategyEnum(),
            SchemasMergeStrategy = settings.GetMergeStrategyEnum(),
        };

        // Merge specifications
        ctx.Status("Merging specifications...");
        var mergeResult = SpecificationService.MergeSpecifications(baseSpec, partSpecs, mergeConfig);

        // Report diagnostics
        var errors = mergeResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var warnings = mergeResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();

        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red]Merge errors ({errors.Count}):[/]");
            foreach (var error in errors)
            {
                AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(error.Message)}");
            }

            AnsiConsole.WriteLine();
        }

        if (warnings.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Merge warnings ({warnings.Count}):[/]");
            foreach (var warning in warnings)
            {
                AnsiConsole.MarkupLine($"  [yellow]•[/] {Markup.Escape(warning.Message)}");
            }

            AnsiConsole.WriteLine();
        }

        if (!mergeResult.IsSuccess || mergeResult.Document == null)
        {
            AnsiConsole.MarkupLine("[red]✗ Merge failed.[/]");
            return 1;
        }

        // Display merge statistics
        DisplayMergeStatistics(mergeResult);

        // Validate if requested
        if (settings.ValidateAfterMerge)
        {
            ctx.Status("Validating merged specification...");
            var validationDiagnostics = SpecificationService.Validate(
                mergeResult.Document,
                settings.OutputPath ?? "merged.yaml",
                ValidateSpecificationStrategy.Standard);

            var validationErrors = validationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (validationErrors.Count > 0)
            {
                AnsiConsole.MarkupLine($"[red]Validation errors ({validationErrors.Count}):[/]");
                foreach (var error in validationErrors)
                {
                    AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(error.Message)}");
                }

                AnsiConsole.MarkupLine("[red]✗ Merged specification has validation errors.[/]");
                return 1;
            }

            AnsiConsole.MarkupLine("[green]✓ Merged specification is valid.[/]");
        }

        // Preview or write output
        if (settings.Preview)
        {
            ctx.Status("Generating preview...");
            var previewContent = SpecificationService.SerializeToYaml(mergeResult.Document);

            // Show first 50 lines of preview
            var previewLines = previewContent.Split('\n').Take(50).ToList();
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Preview (first 50 lines):[/]");
            AnsiConsole.WriteLine();

            foreach (var line in previewLines)
            {
                AnsiConsole.WriteLine(line);
            }

            if (previewContent.Split('\n').Length > 50)
            {
                AnsiConsole.MarkupLine("[dim]... (truncated)[/]");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓ Preview generated successfully.[/]");
        }
        else
        {
            ctx.Status("Writing output file...");
            var outputPath = Path.GetFullPath(settings.OutputPath!);
            var outputContent = string.Equals(settings.OutputFormat, "json", StringComparison.OrdinalIgnoreCase)
                ? SpecificationService.SerializeToJson(mergeResult.Document)
                : SpecificationService.SerializeToYaml(mergeResult.Document);

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            File.WriteAllText(outputPath, outputContent);
            AnsiConsole.MarkupLine($"[green]✓ Merged specification written to:[/] {outputPath}");
        }

        return 0;
    }

    private static List<string> GetFilesToMerge(
        SpecMergeCommandSettings settings)
    {
        var files = new List<string>();

        // If explicit files are provided, use them
        if (!string.IsNullOrWhiteSpace(settings.ExplicitFiles))
        {
            var explicitFiles = settings.ExplicitFiles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .ToList();

            foreach (var file in explicitFiles)
            {
                var fullPath = Path.GetFullPath(file);
                if (File.Exists(fullPath))
                {
                    files.Add(fullPath);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning:[/] File not found: {file}");
                }
            }

            return files;
        }

        // Auto-discover files based on naming convention
        if (!string.IsNullOrWhiteSpace(settings.SpecificationPath))
        {
            var specPath = Path.GetFullPath(settings.SpecificationPath);
            var specDir = Path.GetDirectoryName(specPath) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(specPath);
            var extension = Path.GetExtension(specPath);

            // Add base file first
            files.Add(specPath);

            // Find part files matching pattern: {BaseName}_{PartName}.yaml
            var partPattern = $"{baseName}_*{extension}";
            var partFiles = Directory.GetFiles(specDir, partPattern)
                .Where(f => !f.Equals(specPath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            files.AddRange(partFiles);
        }

        return files;
    }

    private static void DisplayMergeStatistics(MergeResult mergeResult)
    {
        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("[blue]Statistic[/]");
        table.AddColumn("[blue]Value[/]");

        table.AddRow("Files merged", mergeResult.AllFiles.Count.ToString(CultureInfo.InvariantCulture));
        table.AddRow("Total paths", mergeResult.TotalPaths.ToString(CultureInfo.InvariantCulture));
        table.AddRow("Total operations", mergeResult.TotalOperations.ToString(CultureInfo.InvariantCulture));
        table.AddRow("Total schemas", mergeResult.TotalSchemas.ToString(CultureInfo.InvariantCulture));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]OpenAPI Specification Merge Tool[/]");
        AnsiConsole.WriteLine();
    }
}