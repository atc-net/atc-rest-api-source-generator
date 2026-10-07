namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to generate TypeScript client files from an OpenAPI specification.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class GenerateClientTypeScriptCommand : Command<GenerateClientTypeScriptCommandSettings>
{
    /// <summary>
    /// Validates the specification and generates the TypeScript client files, once or in watch mode.
    /// </summary>
    /// <param name="context">The Spectre.Console command context.</param>
    /// <param name="settings">The parsed command-line settings.</param>
    /// <param name="cancellationToken">Cancels the command, which also ends watch mode.</param>
    /// <returns>0 when generation succeeded; otherwise 1.</returns>
    protected override int Execute(
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

        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Specification:[/] {specPath}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Output path:[/] {outputPath}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Validation mode:[/] {config.ValidateSpecificationStrategy}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Enum style:[/] {config.EnumStyle}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Hooks style:[/] {config.HooksStyle}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Client type:[/] {config.HttpClient}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Naming strategy:[/] {config.NamingStrategy}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Convert dates:[/] {config.ConvertDates}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Mutable models:[/] {config.MutableModels}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Zod schemas:[/] {config.GenerateZodSchemas}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Zod runtime validate:[/] {config.ZodRuntimeValidate}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Scaffold:[/] {config.Scaffold}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Branded IDs:[/] {config.BrandedIds}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Enum runtime values:[/] {config.EnumRuntimeValues}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Extensible enums:[/] {config.ExtensibleEnums}");
        if (config.DryRun)
        {
            AnsiConsole.MarkupLine("[blue]Dry run:[/] True");
        }

        AnsiConsole.WriteLine();

        var result = RunGeneration(specPath, outputPath, config, stopwatch);

        // Watch mode: re-run generation when the spec file changes - also after a first run that failed, since the
        // next save is what fixes a specification that does not validate
        if (settings.Watch)
        {
            return RunWatchLoop(specPath, outputPath, config);
        }

        return result;
    }

    private static int RunGeneration(
        string specPath,
        string outputPath,
        TypeScriptClientConfig config,
        Stopwatch stopwatch)
    {
        TypeScriptGenerationResult? generationResult = null;

        var statusResult = AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .Start("Initializing...", ctx =>
            {
                // Step 1: Validate the OpenAPI specification
                ctx.Status("Validating OpenAPI specification...");
                var validationResult = ValidateSpecification(specPath, config.ValidateSpecificationStrategy, config.Overlays);
                if (!validationResult.Success)
                {
                    return 1;
                }

                var parsedDocument = validationResult.Document!;

                // Step 2: Create output directory if it doesn't exist (skip in dry-run)
                if (!config.DryRun)
                {
                    ctx.Status("Creating output directory...");
                    try
                    {
                        if (!Directory.Exists(outputPath))
                        {
                            Directory.CreateDirectory(outputPath);
                            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]\u2713[/] Created output directory: {outputPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]\u2717[/] Error creating output directory: {ex.Message}");
                        return 1;
                    }
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
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]\u2717[/] Error generating TypeScript files: {ex.Message}");
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
        if (generationResult is not null)
        {
            WriteReport(generationResult, outputPath, stopwatch.Elapsed, config.DryRun);
        }

        return 0;
    }

    private static void WriteReport(
        TypeScriptGenerationResult generationResult,
        string outputPath,
        TimeSpan elapsed,
        bool dryRun)
    {
        if (dryRun)
        {
            AnsiConsole.Markup("[dim](dry run)[/] ");
        }

        AnsiConsole.MarkupLine("[green]TypeScript client generation completed successfully.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Models generated:      [blue]{generationResult.ModelCount}[/]");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Enums generated:       [blue]{generationResult.EnumCount}[/]");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Error types generated: [blue]{generationResult.ErrorTypeCount}[/]");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Types generated:       [blue]{generationResult.TypeCount}[/]");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Clients generated:     [blue]{generationResult.ClientCount}[/]");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Hooks generated:       [blue]{generationResult.HookCount}[/]");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Zod schemas generated: [blue]{generationResult.ZodSchemaCount}[/]");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Scaffold generated:    [blue]{generationResult.ScaffoldGenerated}[/]");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  Duration:              [dim]{elapsed.TotalSeconds:F1}s[/]");
        AnsiConsole.WriteLine();

        if (generationResult.Warnings.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] Generation completed with {generationResult.Warnings.Count} warning(s):");
            foreach (var warning in generationResult.Warnings)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [yellow]{warning}[/]");
            }

            AnsiConsole.WriteLine();
        }

        if (generationResult.KeptFiles.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] {generationResult.KeptFiles.Count} file(s) kept as they are - no generated header, so they are somebody's own:");
            foreach (var file in generationResult.KeptFiles)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [yellow]{file}[/]");
            }

            AnsiConsole.WriteLine();
        }

        if (dryRun)
        {
            AnsiConsole.MarkupLine("[dim]Dry run - no files were written.[/]");
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]Output written to: {outputPath}[/]");
        }
    }

    private static int RunWatchLoop(
        string specPath,
        string outputPath,
        TypeScriptClientConfig config)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Watching for changes...[/] Press Ctrl+C to stop.");

        using var exitEvent = new ManualResetEventSlim(false);

        // One regeneration at a time: a change saved during a run regenerates once more after it, never concurrently
        var regeneration = new SerialRunner(() =>
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Change detected at {DateTime.Now:HH:mm:ss}[/]");

            var stopwatch = Stopwatch.StartNew();
            try
            {
                RunGeneration(specPath, outputPath, config, stopwatch);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]\u2717[/] Error during re-generation: {ex.Message}");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Watching for changes...[/] Press Ctrl+C to stop.");
        });
        using var watcher = new SpecificationWatcher(specPath, regeneration.Trigger);

        System.Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            exitEvent.Set();
        };

        exitEvent.Wait();

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Watch mode stopped.[/]");

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

        if (!string.IsNullOrWhiteSpace(settings.HooksStyle) &&
            Enum.TryParse<TypeScriptHooksStyle>(settings.HooksStyle, ignoreCase: true, out var hooksStyle))
        {
            config.HooksStyle = hooksStyle;
        }

        if (!string.IsNullOrWhiteSpace(settings.HooksMode) &&
            Enum.TryParse<TypeScriptHooksMode>(settings.HooksMode, ignoreCase: true, out var hooksMode))
        {
            config.HooksMode = hooksMode;
        }

        if (!string.IsNullOrWhiteSpace(settings.ClientType) &&
            Enum.TryParse<TypeScriptHttpClient>(settings.ClientType, ignoreCase: true, out var httpClient))
        {
            config.HttpClient = httpClient;
        }

        if (!string.IsNullOrWhiteSpace(settings.NamingStrategy) &&
            Enum.TryParse<TypeScriptNamingStrategy>(settings.NamingStrategy, ignoreCase: true, out var namingStrategy))
        {
            config.NamingStrategy = namingStrategy;
        }

        if (settings.ConvertDates)
        {
            config.ConvertDates = true;
        }

        if (settings.NoReadonly)
        {
            config.MutableModels = true;
        }

        if (settings.GenerateZodSchemas)
        {
            config.GenerateZodSchemas = true;
        }

        if (settings.GenerateMswHandlers)
        {
            config.GenerateMswHandlers = true;
        }

        if (settings.Scaffold)
        {
            config.Scaffold = true;
        }

        if (!string.IsNullOrWhiteSpace(settings.PackageName))
        {
            config.PackageName = settings.PackageName;
        }

        if (!string.IsNullOrWhiteSpace(settings.PackageVersion))
        {
            config.PackageVersion = settings.PackageVersion;
        }

        if (settings.BrandedIds)
        {
            config.BrandedIds = true;
        }

        if (settings.ZodRuntimeValidate)
        {
            // Runtime validation needs the schemas to validate against. Implying --zod
            // here is friendlier than erroring on a missing flag combo — the user's
            // intent is clear when they ask for runtime validation.
            config.ZodRuntimeValidate = true;
            config.GenerateZodSchemas = true;
        }

        if (settings.EnumRuntimeValues)
        {
            config.EnumRuntimeValues = true;
        }

        if (settings.NoExtensibleEnums)
        {
            config.ExtensibleEnums = false;
        }

        config.Overlays = settings.Overlays;

        if (settings.DryRun)
        {
            config.DryRun = true;
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
        ValidateSpecificationStrategy strategy,
        IReadOnlyList<string> overlays)
    {
        string yamlContent;
        try
        {
            yamlContent = File.ReadAllText(specPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]\u2717[/] Error reading file: {ex.Message}");
            return (false, null);
        }

        try
        {
            var (parsedDoc, openApiDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(yamlContent, specPath);

            if (parsedDoc is null)
            {
                AnsiConsole.MarkupLine("[red]\u2717[/] Failed to parse OpenAPI specification");

                if (openApiDiagnostic?.Errors is not null)
                {
                    foreach (var error in openApiDiagnostic.Errors)
                    {
                        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red]{error.Message}[/]");
                    }
                }

                return (false, null);
            }

            // Multi-part specifications ({Base}_{Part}.yaml next to the base file - as the build finds them - or an
            // x-multipart list in the base file): merge the parts, so the client covers every part.
            var (merge, merged) = SpecificationService.ReadAndMergeResolved(specPath);
            if (merged?.Document is null)
            {
                AnsiConsole.MarkupLine("[red]✗[/] Failed to merge the multi-part specification:");
                foreach (var diagnostic in merge.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    var location = diagnostic.FilePath is null ? string.Empty : " (" + Path.GetFileName(diagnostic.FilePath) + ")";
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red][[{diagnostic.RuleId}]] {diagnostic.Message + location}[/]");
                }

                return (false, null);
            }

            if (merge.IsMultiPart)
            {
                parsedDoc = merged.Document;
                yamlContent = merged.Content;
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Merged {merge.PartFiles.Count} part file(s) into the specification");
            }

            // The overlays apply to the whole (merged) specification, as in the C# build
            if (overlays.Count > 0)
            {
                var effective = OverlayCliHelper.Apply(yamlContent, overlays);
                if (effective is null)
                {
                    return (false, null);
                }

                (parsedDoc, openApiDiagnostic) = OpenApiDocumentHelper.TryParseYamlWithDiagnostic(effective, specPath);
                if (parsedDoc is null)
                {
                    AnsiConsole.MarkupLine("[red]✗[/] The overlays make a document that is no OpenAPI specification");
                    return (false, null);
                }

                yamlContent = effective;
            }

            var diagnostics = OpenApiDocumentValidator.Validate(
                strategy,
                parsedDoc,
                openApiDiagnostic?.Errors ?? [],
                specPath,
                yamlContent,
                openApiDiagnostic?.Warnings);

            var errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (errors.Count > 0)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]\u2717[/] Validation failed with {errors.Count} error(s):");
                foreach (var error in errors)
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red][[{error.RuleId}]] {error.Message}[/]");
                }

                return (false, null);
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
                AnsiConsole.MarkupLine("[green]\u2713[/] Validation passed - no issues found");
            }

            return (true, parsedDoc);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]\u2717[/] Error parsing OpenAPI specification: {ex.Message}");
            return (false, null);
        }
    }
}