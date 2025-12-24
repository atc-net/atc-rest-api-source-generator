namespace Atc.Rest.Api.CliGenerator.Commands;

/// <summary>
/// Command to generate a client project from an OpenAPI specification.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class GenerateClientCommand : Command<GenerateClientCommandSettings>
{
    private const string MarkerFileName = ".atc-rest-api-client-contracts";

    public override int Execute(
        CommandContext context,
        GenerateClientCommandSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        WriteHeader();

        var specPath = Path.GetFullPath(settings.SpecificationPath);
        var outputPath = Path.GetFullPath(settings.OutputPath);

        // Use client project name override if provided
        var projectName = !string.IsNullOrWhiteSpace(settings.ClientProjectName)
            ? settings.ClientProjectName
            : settings.ProjectName;

        // Load options from file (auto-discovery or explicit path)
        var options = Helpers.ApiOptionsHelper.LoadOptions(settings.OptionsPath, specPath);

        // Apply CLI argument overrides
        var clientConfig = ApplyCliOverrides(options, settings);

        AnsiConsole.MarkupLine($"[blue]Specification:[/] {specPath}");
        AnsiConsole.MarkupLine($"[blue]Output path:[/] {outputPath}");
        AnsiConsole.MarkupLine($"[blue]Project name:[/] {projectName}");
        AnsiConsole.MarkupLine($"[blue]Validation mode:[/] {clientConfig.ValidateSpecificationStrategy}");
        AnsiConsole.MarkupLine($"[blue]Generation mode:[/] {clientConfig.GenerationMode}");

        if (!string.IsNullOrWhiteSpace(clientConfig.Namespace))
        {
            AnsiConsole.MarkupLine($"[blue]Namespace:[/] {clientConfig.Namespace}");
        }

        AnsiConsole.WriteLine();

        // Track for statistics
        OpenApiDocument? parsedDocument = null;
        IReadOnlyList<DiagnosticMessage> validationDiagnostics = [];
        var specFileName = Path.GetFileName(specPath);

        var statusResult = AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .Start("Initializing...", ctx =>
            {
                // Step 1: Validate the OpenAPI specification
                ctx.Status("Validating OpenAPI specification...");
                var validationResult = ValidateSpecificationWithStats(specPath, clientConfig.ValidateSpecificationStrategy);
                if (!validationResult.Success)
                {
                    return 1;
                }

                parsedDocument = validationResult.Document;
                validationDiagnostics = validationResult.Diagnostics;

                // Step 2: Check for conflicting project files in output directory
                if (!ValidateOutputDirectory(outputPath, projectName))
                {
                    return 1;
                }

                // Step 3: Create output directory if it doesn't exist
                ctx.Status("Creating output directory...");
                try
                {
                    if (!Directory.Exists(outputPath))
                    {
                        Directory.CreateDirectory(outputPath);
                        AnsiConsole.MarkupLine($"[green]✓[/] Created output directory: {outputPath}");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Error creating output directory: {ex.Message}");
                    return 1;
                }

                // Step 4: Copy the YAML specification file to output directory
                ctx.Status("Copying specification file...");
                if (!CopySpecificationFile(specPath, outputPath, specFileName))
                {
                    return 1;
                }

                // Step 5: Create or update marker file with client config
                ctx.Status("Creating marker file...");
                if (!CreateOrUpdateMarkerFile(outputPath, clientConfig))
                {
                    return 1;
                }

                // Step 6: Create or update project file (using local YAML file name)
                ctx.Status("Creating project file...");
                if (!CreateOrUpdateProjectFile(outputPath, projectName, specFileName))
                {
                    return 1;
                }

                return 0;
            });

        // Output results after Status context is complete (prevents spinner text bleeding)
        if (statusResult != 0)
        {
            return statusResult;
        }

        stopwatch.Stop();
        AnsiConsole.MarkupLine("[green]Client project setup completed successfully.[/]");

        // Output generation report
        if (parsedDocument is not null)
        {
            var stats = StatisticsCollector.CollectFromOpenApiDocument(
                parsedDocument,
                specFileName,
                "Client",
                validationDiagnostics);

            var finalStats = stats with
            {
                Duration = stopwatch.Elapsed,
                ProjectsCreated = [projectName],
            };

            ReportService.WriteToConsole(finalStats);

            // Write report file if --report flag is passed
            if (settings.GenerateReport)
            {
                var reportPath = Path.Combine(outputPath, ".generation-report.md");
                ReportService.WriteToFile(finalStats, reportPath);
                AnsiConsole.MarkupLine($"[dim]Report written to: {reportPath}[/]");
            }
        }

        AnsiConsole.MarkupLine($"[dim]Run 'dotnet build' in {outputPath} to generate the client code.[/]");

        return 0;
    }

    private static ClientConfig ApplyCliOverrides(
        ApiGeneratorOptions options,
        GenerateClientCommandSettings settings)
    {
        // Start with config from options file
        var config = options.ToClientConfig();

        // Override validation strategy if --no-strict is specified
        if (settings.DisableStrictMode)
        {
            config.ValidateSpecificationStrategy = ValidateSpecificationStrategy.Standard;
        }

        // Override include deprecated if specified
        if (settings.IncludeDeprecated)
        {
            config.IncludeDeprecated = true;
        }

        // Override namespace: client-namespace takes precedence over --namespace
        if (!string.IsNullOrWhiteSpace(settings.ClientNamespace))
        {
            config.Namespace = settings.ClientNamespace;
        }
        else if (!string.IsNullOrWhiteSpace(settings.Namespace))
        {
            config.Namespace = settings.Namespace;
        }

        // Override generation mode if specified
        if (!string.IsNullOrWhiteSpace(settings.GenerationMode) &&
            Enum.TryParse<GenerationModeType>(settings.GenerationMode, ignoreCase: true, out var mode))
        {
            config.GenerationMode = mode;
        }

        // Override client suffix if specified
        if (!string.IsNullOrWhiteSpace(settings.ClientSuffix))
        {
            config.ClientSuffix = settings.ClientSuffix;
        }

        // Disable OAuth if --no-oauth is specified
        if (settings.DisableOAuth)
        {
            config.GenerateOAuthTokenManagement = false;
        }

        return config;
    }

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Client Project Generator[/]");
        AnsiConsole.WriteLine();
    }

    private static (bool Success, OpenApiDocument? Document, IReadOnlyList<DiagnosticMessage> Diagnostics) ValidateSpecificationWithStats(
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

            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            if (errors.Count > 0)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Validation failed with {errors.Count} error(s):");
                foreach (var error in errors)
                {
                    AnsiConsole.MarkupLine($"  [red][[{error.RuleId}]] {Markup.Escape(error.Message)}[/]");
                }

                return (false, parsedDoc, diagnostics);
            }

            var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
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

    private static bool ValidateOutputDirectory(
        string outputPath,
        string projectName)
    {
        if (!Directory.Exists(outputPath))
        {
            return true;
        }

        var existingCsprojFiles = Directory.GetFiles(outputPath, "*.csproj");
        var expectedCsprojName = $"{projectName}.csproj";

        foreach (var csprojFile in existingCsprojFiles)
        {
            var fileName = Path.GetFileName(csprojFile);
            if (!fileName.Equals(expectedCsprojName, StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Output directory contains a different project file: {fileName}");
                AnsiConsole.MarkupLine($"  [red]Expected:[/] {expectedCsprojName}");
                AnsiConsole.MarkupLine("  [yellow]Please use a different output directory or project name.[/]");
                return false;
            }
        }

        return true;
    }

    private static bool CopySpecificationFile(
        string sourcePath,
        string outputPath,
        string fileName)
    {
        var destinationPath = Path.Combine(outputPath, fileName);

        try
        {
            if (File.Exists(destinationPath))
            {
                // Check if files are the same (by content hash or last write time)
                var sourceLastWrite = File.GetLastWriteTimeUtc(sourcePath);
                var destLastWrite = File.GetLastWriteTimeUtc(destinationPath);

                if (sourceLastWrite <= destLastWrite)
                {
                    AnsiConsole.MarkupLine($"[dim]✓[/] Specification file up to date: {fileName}");
                    return true;
                }

                // Source is newer, update the file
                File.Copy(sourcePath, destinationPath, overwrite: true);
                AnsiConsole.MarkupLine($"[green]✓[/] Updated specification file: {fileName}");
            }
            else
            {
                File.Copy(sourcePath, destinationPath);
                AnsiConsole.MarkupLine($"[green]✓[/] Copied specification file: {fileName}");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error copying specification file: {ex.Message}");
            return false;
        }
    }

    private static bool CreateOrUpdateMarkerFile(
        string outputPath,
        ClientConfig config)
    {
        var markerFilePath = Path.Combine(outputPath, MarkerFileName);

        try
        {
            var jsonOptions = JsonSerializerOptionsFactory.Create();

            var json = JsonSerializer.Serialize(config, jsonOptions);

            if (File.Exists(markerFilePath))
            {
                var existingContent = File.ReadAllText(markerFilePath);
                if (existingContent.Trim() == json.Trim())
                {
                    AnsiConsole.MarkupLine($"[dim]✓[/] Marker file up to date: {MarkerFileName}");
                    return true;
                }

                File.WriteAllText(markerFilePath, json);
                AnsiConsole.MarkupLine($"[green]✓[/] Updated marker file: {MarkerFileName}");
            }
            else
            {
                File.WriteAllText(markerFilePath, json);
                AnsiConsole.MarkupLine($"[green]✓[/] Created marker file: {MarkerFileName}");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error creating marker file: {ex.Message}");
            return false;
        }
    }

    private static bool CreateOrUpdateProjectFile(
        string outputPath,
        string projectName,
        string specFileName)
    {
        var csprojPath = Path.Combine(outputPath, $"{projectName}.csproj");

        try
        {
            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateProjectFileContent(specFileName);
                File.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLine($"[green]✓[/] Created project file: {projectName}.csproj");
            }
            else
            {
                var updated = UpdateProjectFileIfNeeded(csprojPath, specFileName);
                if (updated)
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] Updated project file: {projectName}.csproj");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[dim]✓[/] Project file up to date: {projectName}.csproj");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error creating/updating project file: {ex.Message}");
            return false;
        }
    }

    private static string GenerateProjectFileContent(string specFileName)
        => $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\Generated</CompilerGeneratedFilesOutputPath>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Atc.Rest.Api.SourceGenerator" Version="*" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>

              <ItemGroup>
                <AdditionalFiles Include="{specFileName}" />
                <AdditionalFiles Include=".atc-rest-api-client-contracts" />
              </ItemGroup>

            </Project>
            """;

    private static bool UpdateProjectFileIfNeeded(
        string csprojPath,
        string specFileName)
    {
        var content = File.ReadAllText(csprojPath);
        var originalContent = content;

        // Check if AdditionalFiles entries exist
        var hasYamlInclude = content.Contains($"Include=\"{specFileName}\"", StringComparison.OrdinalIgnoreCase);
        var hasMarkerInclude = content.Contains(MarkerFileName, StringComparison.OrdinalIgnoreCase);

        if (hasYamlInclude && hasMarkerInclude)
        {
            return false;
        }

        // If AdditionalFiles ItemGroup exists but is missing entries, we need to update
        if (content.Contains("<AdditionalFiles", StringComparison.OrdinalIgnoreCase))
        {
            // Find the AdditionalFiles ItemGroup and add missing entries
            if (!hasYamlInclude)
            {
                var insertPosition = content.LastIndexOf("</ItemGroup>", StringComparison.OrdinalIgnoreCase);
                var additionalFilesPosition = content.LastIndexOf("<AdditionalFiles", insertPosition, StringComparison.OrdinalIgnoreCase);
                if (insertPosition >= 0 && additionalFilesPosition >= 0)
                {
                    var yamlEntry = $"    <AdditionalFiles Include=\"{specFileName}\" />\n";
                    content = content.Insert(insertPosition, yamlEntry);
                }
            }

            if (!hasMarkerInclude)
            {
                var insertPosition = content.LastIndexOf("</ItemGroup>", StringComparison.OrdinalIgnoreCase);
                var additionalFilesPosition = content.LastIndexOf("<AdditionalFiles", insertPosition, StringComparison.OrdinalIgnoreCase);
                if (insertPosition >= 0 && additionalFilesPosition >= 0)
                {
                    var markerEntry = $"    <AdditionalFiles Include=\"{MarkerFileName}\" />\n";
                    content = content.Insert(insertPosition, markerEntry);
                }
            }
        }
        else
        {
            // Add a new ItemGroup before the closing Project tag
            var insertPosition = content.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
            if (insertPosition >= 0)
            {
                var newItemGroup = $"""

                  <ItemGroup>
                    <AdditionalFiles Include="{specFileName}" />
                    <AdditionalFiles Include="{MarkerFileName}" />
                  </ItemGroup>

                """;
                content = content.Insert(insertPosition, newItemGroup);
            }
        }

        if (content != originalContent)
        {
            File.WriteAllText(csprojPath, content);
            return true;
        }

        return false;
    }
}