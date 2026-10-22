namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to validate an OpenAPI specification file.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class SpecValidateCommand : Command<SpecValidateCommandSettings>
{
    /// <summary>
    /// Validates the specification (or multi-part set) with the strict or standard strategy and prints the diagnostics.
    /// </summary>
    /// <param name="context">The Spectre.Console command context.</param>
    /// <param name="settings">The parsed command-line settings.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>0 when the specification has no errors; otherwise 1.</returns>
    public override int Execute(
        CommandContext context,
        SpecValidateCommandSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader();

        var strategy = settings.StrictMode
            ? ValidateSpecificationStrategy.Strict
            : ValidateSpecificationStrategy.Standard;

        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Validation mode:[/] {strategy}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Multi-part mode:[/] {(settings.UseMultiPart ? "Yes" : "No")}");
        AnsiConsole.WriteLine();

        return AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .Start("Validating OpenAPI specification...", ctx =>
            {
                try
                {
                    if (settings.UseMultiPart)
                    {
                        return ExecuteMultiPartValidation(settings, strategy, ctx);
                    }
                    else
                    {
                        return ExecuteSingleFileValidation(settings, strategy, ctx);
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error: {ex.Message}");
                    return 1;
                }
            });
    }

    private static int ExecuteSingleFileValidation(
        SpecValidateCommandSettings settings,
        ValidateSpecificationStrategy strategy,
        StatusContext ctx)
    {
        var specPath = Path.GetFullPath(settings.SpecificationPath);
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Specification:[/] {specPath}");
        AnsiConsole.WriteLine();

        // Read the specification file
        string yamlContent;
        try
        {
            yamlContent = File.ReadAllText(specPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error reading file: {ex.Message}");
            return 1;
        }

        // The overlays apply before the parse, as in the build
        if (settings.Overlays.Length > 0 && OverlayCliHelper.Apply(yamlContent, settings.Overlays) is { } effective)
        {
            yamlContent = effective;
        }
        else if (settings.Overlays.Length > 0)
        {
            return 1;
        }

        // Parse and validate the OpenAPI document
        ctx.Status("Parsing and validating the OpenAPI document...");
        var result = SpecificationPipeline.ParseAndValidate(specPath, yamlContent, strategy);
        if (result.Document is null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Failed to parse OpenAPI specification");
            foreach (var error in result.Diagnostics)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red]•[/] {error.Message}");
            }

            return 1;
        }

        return DisplayValidationResults(result.Diagnostics);
    }

    private static int ExecuteMultiPartValidation(
        SpecValidateCommandSettings settings,
        ValidateSpecificationStrategy strategy,
        StatusContext ctx)
    {
        // Determine files to validate
        ctx.Status("Discovering specification files...");
        var filesToValidate = GetFilesToValidate(settings);

        if (filesToValidate.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No files found to validate.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[blue]Files to validate:[/]");
        foreach (var file in filesToValidate)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] {Path.GetFileName(file)}");
        }

        AnsiConsole.WriteLine();

        // Read base file
        ctx.Status("Reading base specification...");
        var baseFile = filesToValidate[0];
        var baseSpec = SpecificationService.ReadFromFile(baseFile);

        if (baseSpec.Document is null)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Failed to parse base specification: {Path.GetFileName(baseFile)}");
            return 1;
        }

        // If only one file, validate it directly
        if (filesToValidate.Count == 1 && settings.Overlays.Length == 0)
        {
            ctx.Status("Running validation rules...");
            var single = SpecificationPipeline.Validate(baseSpec.Document, [(baseFile, baseSpec.Content)], strategy);
            return DisplayValidationResults(single.Diagnostics);
        }

        // Read part files
        ctx.Status("Reading part files...");
        var partSpecs = new List<SpecificationFile>();
        foreach (var partFile in filesToValidate.Skip(1))
        {
            var spec = SpecificationService.ReadFromFile(partFile);
            if (spec.Document is null)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Warning:[/] Failed to parse part file: {Path.GetFileName(partFile)}");
                continue;
            }

            partSpecs.Add(spec);
        }

        // Merge specifications
        ctx.Status("Merging specifications...");
        var mergeResult = SpecificationService.MergeSpecifications(
            baseSpec,
            partSpecs,
            MultiPartConfiguration.Default);

        // Report merge diagnostics
        var mergeErrors = mergeResult
            .Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (mergeErrors.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]Merge errors ({mergeErrors.Count}):[/]");
            foreach (var error in mergeErrors)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red]•[/] {error.Message}");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]✗ Merge failed - cannot validate.[/]");
            return 1;
        }

        if (!mergeResult.IsSuccess || mergeResult.Document is null)
        {
            AnsiConsole.MarkupLine("[red]✗ Merge failed - cannot validate.[/]");
            return 1;
        }

        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Merged:[/] {mergeResult.AllFiles.Count.ToString(CultureInfo.InvariantCulture)} files, {mergeResult.TotalPaths.ToString(CultureInfo.InvariantCulture)} paths, {mergeResult.TotalSchemas.ToString(CultureInfo.InvariantCulture)} schemas");
        AnsiConsole.WriteLine();

        // The overlays see the whole merged document, as in the build
        var document = mergeResult.Document;
        if (settings.Overlays.Length > 0)
        {
            var effective = OverlayCliHelper.Apply(SpecificationService.SerializeToJson(document), settings.Overlays);
            if (effective is null || OpenApiDocumentHelper.TryParseYamlWithDiagnostic(effective, baseFile).Document is not { } overlaid)
            {
                return 1;
            }

            document = overlaid;
        }

        // Validate the merged document: each diagnostic on the line of the file - the base or a part - that declares
        // what it is about
        ctx.Status("Running validation rules on merged specification...");
        var merged = SpecificationPipeline.Validate(
            document,
            [(baseFile, baseSpec.Content), .. partSpecs.Select(p => (p.FilePath, p.Content))],
            strategy);
        return DisplayValidationResults(merged.Diagnostics);
    }

    private static List<string> GetFilesToValidate(
        SpecValidateCommandSettings settings)
    {
        var files = new List<string>();

        // If explicit files are provided, use them
        if (!string.IsNullOrWhiteSpace(settings.ExplicitFiles))
        {
            var explicitFiles = settings
                .ExplicitFiles
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
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
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Warning:[/] File not found: {file}");
                }
            }

            return files;
        }

        // Auto-discover files based on naming convention
        if (!string.IsNullOrWhiteSpace(settings.SpecificationPath))
        {
            var specPath = Path.GetFullPath(settings.SpecificationPath);

            // The base file first, then its parts as the build finds them
            files.Add(specPath);
            files.AddRange(SpecificationService.DiscoverPartFilePaths(specPath));
        }

        return files;
    }

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]OpenAPI Specification Validator[/]");
        AnsiConsole.WriteLine();
    }

    private static int DisplayValidationResults(
        IReadOnlyList<DiagnosticMessage> diagnostics)
    {
        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        var warnings = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .ToList();

        switch (errors.Count)
        {
            case 0 when warnings.Count == 0:
                AnsiConsole.MarkupLine("[green]✓ Validation passed - no issues found.[/]");
                return 0;
            case > 0:
            {
                // Display errors with rich formatting
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]Errors ({errors.Count}):[/]");
                AnsiConsole.WriteLine();
                foreach (var error in errors)
                {
                    DisplayRichDiagnostic(error);
                }

                break;
            }
        }

        // Display warnings with rich formatting
        if (warnings.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Warnings ({warnings.Count}):[/]");
            AnsiConsole.WriteLine();
            foreach (var warning in warnings)
            {
                DisplayRichDiagnostic(warning);
            }
        }

        // Summary
        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗ Validation failed with {errors.Count} error(s) and {warnings.Count} warning(s).[/]");
            return 1;
        }

        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓ Validation passed with {warnings.Count} warning(s).[/]");
        return 0;
    }

    private static void DisplayRichDiagnostic(DiagnosticMessage diagnostic)
    {
        // Use Spectre.Console markup for rich display
        var markup = DiagnosticMessageFormatter.FormatSpectreMarkup(diagnostic);
        AnsiConsole.Markup(markup);
        AnsiConsole.WriteLine();
    }
}