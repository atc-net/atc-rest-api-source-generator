namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to generate TypeScript client files from an OpenAPI specification.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class GenerateClientTypeScriptCommand : Command<GenerateClientTypeScriptCommandSettings>
{
    public override int Execute(
        CommandContext context,
        GenerateClientTypeScriptCommandSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        WriteHeader();

        var specPath = Path.GetFullPath(settings.SpecificationPath);
        var outputPath = Path.GetFullPath(settings.OutputPath);

        // Build configuration from CLI options
        var config = BuildConfig(settings);

        AnsiConsole.MarkupLine($"[blue]Specification:[/] {specPath}");
        AnsiConsole.MarkupLine($"[blue]Output path:[/] {outputPath}");
        AnsiConsole.MarkupLine($"[blue]Validation mode:[/] {config.ValidateSpecificationStrategy}");
        AnsiConsole.MarkupLine($"[blue]Enum style:[/] {config.EnumStyle}");
        AnsiConsole.WriteLine();

        TypeScriptGenerationResult? generationResult = null;

        var statusResult = AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .Start("Initializing...", ctx =>
            {
                // Step 1: Validate the OpenAPI specification
                ctx.Status("Validating OpenAPI specification...");
                var validationResult = ValidateSpecification(specPath, config.ValidateSpecificationStrategy);
                if (!validationResult.Success)
                {
                    return 1;
                }

                var parsedDocument = validationResult.Document!;

                // Step 2: Create output directory if it doesn't exist
                ctx.Status("Creating output directory...");
                try
                {
                    if (!Directory.Exists(outputPath))
                    {
                        Directory.CreateDirectory(outputPath);
                        AnsiConsole.MarkupLine($"[green]\u2713[/] Created output directory: {outputPath}");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]\u2717[/] Error creating output directory: {ex.Message}");
                    return 1;
                }

                // Step 3: Generate TypeScript files
                ctx.Status("Generating TypeScript client...");
                try
                {
                    generationResult = TypeScriptClientGenerationService.Generate(
                        parsedDocument,
                        outputPath,
                        config);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]\u2717[/] Error generating TypeScript files: {ex.Message}");
                    return 1;
                }

                return 0;
            });

        if (statusResult != 0)
        {
            return statusResult;
        }

        stopwatch.Stop();

        // Report results
        if (generationResult != null)
        {
            AnsiConsole.MarkupLine("[green]TypeScript client generation completed successfully.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"  Models generated:      [blue]{generationResult.ModelCount}[/]");
            AnsiConsole.MarkupLine($"  Enums generated:       [blue]{generationResult.EnumCount}[/]");
            AnsiConsole.MarkupLine($"  Error types generated: [blue]{generationResult.ErrorTypeCount}[/]");
            AnsiConsole.MarkupLine($"  Types generated:       [blue]{generationResult.TypeCount}[/]");
            AnsiConsole.MarkupLine($"  Clients generated:     [blue]{generationResult.ClientCount}[/]");
            AnsiConsole.MarkupLine($"  Duration:              [dim]{stopwatch.Elapsed.TotalSeconds:F1}s[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Output written to: {outputPath}[/]");
        }

        return 0;
    }

    private static TypeScriptClientConfig BuildConfig(
        GenerateClientTypeScriptCommandSettings settings)
    {
        var config = new TypeScriptClientConfig();

        if (settings.DisableStrictMode)
        {
            config.ValidateSpecificationStrategy = ValidateSpecificationStrategy.Standard;
        }

        if (settings.IncludeDeprecated)
        {
            config.IncludeDeprecated = true;
        }

        if (!string.IsNullOrWhiteSpace(settings.EnumStyle) &&
            Enum.TryParse<TypeScriptEnumStyle>(settings.EnumStyle, ignoreCase: true, out var enumStyle))
        {
            config.EnumStyle = enumStyle;
        }

        return config;
    }

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]TypeScript Client Generator[/]");
        AnsiConsole.WriteLine();
    }

    private static (bool Success, OpenApiDocument? Document) ValidateSpecification(
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
            AnsiConsole.MarkupLine($"[red]\u2717[/] Error reading file: {ex.Message}");
            return (false, null);
        }

        try
        {
            var (parsedDoc, openApiDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, specPath);

            if (parsedDoc == null)
            {
                AnsiConsole.MarkupLine("[red]\u2717[/] Failed to parse OpenAPI specification");

                if (openApiDiagnostic?.Errors != null)
                {
                    foreach (var error in openApiDiagnostic.Errors)
                    {
                        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(error.Message)}[/]");
                    }
                }

                return (false, null);
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
                AnsiConsole.MarkupLine($"[red]\u2717[/] Validation failed with {errors.Count} error(s):");
                foreach (var error in errors)
                {
                    AnsiConsole.MarkupLine($"  [red][[{error.RuleId}]] {Markup.Escape(error.Message)}[/]");
                }

                return (false, null);
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
                AnsiConsole.MarkupLine("[green]\u2713[/] Validation passed - no issues found");
            }

            return (true, parsedDoc);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]\u2717[/] Error parsing OpenAPI specification: {ex.Message}");
            return (false, null);
        }
    }
}