namespace Atc.Rest.Api.CliGenerator.Commands;

/// <summary>
/// Command to validate an OpenAPI specification file.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class SpecValidateCommand : Command<SpecValidateCommandSettings>
{
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

        AnsiConsole.MarkupLine($"[blue]Validation mode:[/] {strategy}");
        AnsiConsole.MarkupLine($"[blue]Multi-part mode:[/] {(settings.UseMultiPart ? "Yes" : "No")}");
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
                    AnsiConsole.MarkupLine($"[red]✗[/] Error: {ex.Message}");
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
        AnsiConsole.MarkupLine($"[blue]Specification:[/] {specPath}");
        AnsiConsole.WriteLine();

        // Read the specification file
        string yamlContent;
        try
        {
            yamlContent = File.ReadAllText(specPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error reading file: {ex.Message}");
            return 1;
        }

        // Parse the OpenAPI document
        ctx.Status("Parsing OpenAPI document...");

        var (openApiDoc, openApiDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, specPath);

        if (openApiDoc == null)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Failed to parse OpenAPI specification");

            if (openApiDiagnostic?.Errors != null)
            {
                foreach (var error in openApiDiagnostic.Errors)
                {
                    AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(error.Message)}");
                }
            }

            return 1;
        }

        // Validate the document
        ctx.Status("Running validation rules...");
        var diagnostics = OpenApiDocumentValidator.Validate(
            strategy,
            openApiDoc,
            openApiDiagnostic?.Errors ?? [],
            specPath);

        // Display results
        return DisplayValidationResults(diagnostics);
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
            AnsiConsole.MarkupLine($"  [dim]•[/] {Path.GetFileName(file)}");
        }

        AnsiConsole.WriteLine();

        // Read base file
        ctx.Status("Reading base specification...");
        var baseFile = filesToValidate[0];
        var baseSpec = SpecificationService.ReadFromFile(baseFile);

        if (baseSpec.Document == null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to parse base specification: {Path.GetFileName(baseFile)}");
            return 1;
        }

        // If only one file, validate it directly
        if (filesToValidate.Count == 1)
        {
            ctx.Status("Running validation rules...");
            var singleDiagnostics = SpecificationService.Validate(
                baseSpec.Document,
                baseFile,
                strategy);
            return DisplayValidationResults(singleDiagnostics);
        }

        // Read part files
        ctx.Status("Reading part files...");
        var partSpecs = new List<SpecificationFile>();
        foreach (var partFile in filesToValidate.Skip(1))
        {
            var spec = SpecificationService.ReadFromFile(partFile);
            if (spec.Document == null)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to parse part file: {Path.GetFileName(partFile)}");
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
            AnsiConsole.MarkupLine($"[red]Merge errors ({mergeErrors.Count}):[/]");
            foreach (var error in mergeErrors)
            {
                AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(error.Message)}");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]✗ Merge failed - cannot validate.[/]");
            return 1;
        }

        if (!mergeResult.IsSuccess || mergeResult.Document == null)
        {
            AnsiConsole.MarkupLine("[red]✗ Merge failed - cannot validate.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[blue]Merged:[/] {mergeResult.AllFiles.Count.ToString(CultureInfo.InvariantCulture)} files, {mergeResult.TotalPaths.ToString(CultureInfo.InvariantCulture)} paths, {mergeResult.TotalSchemas.ToString(CultureInfo.InvariantCulture)} schemas");
        AnsiConsole.WriteLine();

        // Validate the merged document
        ctx.Status("Running validation rules on merged specification...");
        var diagnostics = SpecificationService.Validate(
            mergeResult.Document,
            baseFile,
            strategy);

        // Display results
        return DisplayValidationResults(diagnostics);
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
            var partFiles = Directory
                .GetFiles(specDir, partPattern)
                .Where(f => !f.Equals(specPath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            files.AddRange(partFiles);
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
                AnsiConsole.MarkupLine($"[red]Errors ({errors.Count}):[/]");
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
            AnsiConsole.MarkupLine($"[yellow]Warnings ({warnings.Count}):[/]");
            AnsiConsole.WriteLine();
            foreach (var warning in warnings)
            {
                DisplayRichDiagnostic(warning);
            }
        }

        // Summary
        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red]✗ Validation failed with {errors.Count} error(s) and {warnings.Count} warning(s).[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]✓ Validation passed with {warnings.Count} warning(s).[/]");
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