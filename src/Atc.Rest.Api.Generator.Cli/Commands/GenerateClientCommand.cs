namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to generate a client project from an OpenAPI specification.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class GenerateClientCommand : Command<GenerateClientCommandSettings>
{
    private readonly INugetPackageVersionService nugetService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateClientCommand"/> class.
    /// </summary>
    /// <param name="nugetService">Resolves the latest package versions written into the generated project file.</param>
    public GenerateClientCommand(INugetPackageVersionService nugetService)
    {
        this.nugetService = nugetService;
    }

    /// <summary>
    /// Validates the specification, scaffolds the client project and generates the client code into it.
    /// </summary>
    /// <param name="context">The Spectre.Console command context.</param>
    /// <param name="settings">The parsed command-line settings.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>0 when generation succeeded; otherwise 1.</returns>
    protected override int Execute(
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

        // Load options from file (auto-discovery or explicit path); a file that cannot be read stops the command
        ApiGeneratorOptions options;
        try
        {
            options = ApiOptionsHelper.LoadOptions(settings.OptionsPath, specPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }

        // Project name: --client-project, then scaffolding.clientProjectName from the options file, then --name
        var projectName = new[] { settings.ClientProjectName, options.Scaffolding.ClientProjectName }
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? settings.ProjectName;

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

        var statusResult = AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .Start("Initializing...", ctx =>
            {
                // Step 0: Resolve package versions from NuGet (with fallback)
                ctx.Status("Resolving package versions...");
                var sourceGeneratorVersion = TaskHelper.RunSync(() => nugetService.GetLatestVersionOrFallbackAsync(
                    PackageVersionDefaults.SourceGeneratorPackageId,
                    PackageVersionDefaults.SourceGeneratorFallback,
                    cancellationToken));
                var restClientVersion = TaskHelper.RunSync(() => nugetService.GetLatestVersionOrFallbackAsync(
                    PackageVersionDefaults.RestClientPackageId,
                    PackageVersionDefaults.RestClientMinFallback,
                    cancellationToken));

                // Step 1: Validate the OpenAPI specification
                ctx.Status("Validating OpenAPI specification...");
                var validationResult = SpecificationValidationHelper.ValidateSpecificationWithStats(specPath, clientConfig.ValidateSpecificationStrategy, settings.Overlays);
                if (!validationResult.Success)
                {
                    return 1;
                }

                parsedDocument = validationResult.Document;
                validationDiagnostics = validationResult.Diagnostics;

                // A multi-part specification ({Base}_{Part}.yaml): the parts' operations, schemas and retry
                // settings count too (e.g. for the resilience package and the statistics).
                var (merge, merged) = SpecificationService.ReadAndMergeResolved(specPath);
                if (!merge.IsSuccess)
                {
                    foreach (var diagnostic in merge.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        var location = diagnostic.FilePath is null ? string.Empty : " (" + Path.GetFileName(diagnostic.FilePath) + ")";
                        AnsiConsole.MarkupLine($"  [red][[{diagnostic.RuleId}]] {Markup.Escape(diagnostic.Message + location)}[/]");
                    }

                    return 1;
                }

                if (merge.IsMultiPart && merged?.Document is not null)
                {
                    parsedDocument = merged.Document;
                }

                // Resolve resilience package if spec has retry configuration
                Version? resilienceVersion = null;
                if (parsedDocument is not null && parsedDocument.HasRetryConfiguration())
                {
                    resilienceVersion = TaskHelper.RunSync(() => nugetService.GetLatestVersionOrFallbackAsync(
                        PackageVersionDefaults.ResiliencePackageId,
                        PackageVersionDefaults.ResilienceMinFallback,
                        cancellationToken));
                }

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

                // Multi-part specification: the part files ({Base}_{Part}.yaml) go along, or the build
                // would generate a client for the base file only.
                var partFileNames = new List<string>();
                foreach (var partFile in SpecificationService.DiscoverPartFiles(specPath))
                {
                    var partFileName = Path.GetFileName(partFile.FilePath);
                    if (!CopySpecificationFile(partFile.FilePath, outputPath, partFileName))
                    {
                        return 1;
                    }

                    partFileNames.Add(partFileName);
                }

                // The overlays go along, listed in the marker: the build applies them to the specification
                if (!CopyOverlays(settings.Overlays, outputPath, partFileNames, out var overlayFileNames))
                {
                    return 1;
                }

                if (overlayFileNames.Count > 0)
                {
                    clientConfig.Overlays = overlayFileNames;
                }

                // Step 5: Create or update marker file with client config
                ctx.Status("Creating marker file...");
                if (!CreateOrUpdateMarkerFile(outputPath, clientConfig))
                {
                    return 1;
                }

                // Step 6: Create or update project file (using local YAML file name)
                ctx.Status("Creating project file...");
                if (!CreateOrUpdateProjectFile(outputPath, projectName, specFileName, partFileNames, sourceGeneratorVersion, restClientVersion, resilienceVersion))
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

        // Namespace: --client-namespace, scaffolding.clientNamespace, --namespace, then client.namespace from the options file
        if (!string.IsNullOrWhiteSpace(settings.ClientNamespace))
        {
            config.Namespace = settings.ClientNamespace;
        }
        else if (!string.IsNullOrWhiteSpace(options.Scaffolding.ClientNamespace))
        {
            config.Namespace = options.Scaffolding.ClientNamespace;
        }
        else if (!string.IsNullOrWhiteSpace(settings.Namespace))
        {
            config.Namespace = settings.Namespace;
        }
        else if (string.IsNullOrWhiteSpace(config.Namespace))
        {
            config.Namespace = ProjectNameHelper.ExtractSolutionName(settings.ProjectName);
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

        // Override client granularity if specified
        if (!string.IsNullOrWhiteSpace(settings.ClientGranularity) &&
            Enum.TryParse<ClientGranularityType>(settings.ClientGranularity, ignoreCase: true, out var granularity))
        {
            config.ClientGranularity = granularity;
        }

        // Override client name if specified
        if (!string.IsNullOrWhiteSpace(settings.ClientName))
        {
            config.ClientName = settings.ClientName;
        }

        // Disable OAuth if --no-oauth is specified
        if (settings.DisableOAuth)
        {
            config.GenerateOAuthTokenManagement = false;
        }

        if (settings.NoExtensibleEnums)
        {
            config.ExtensibleEnums = false;
        }

        return config;
    }

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Client Project Generator[/]");
        AnsiConsole.WriteLine();
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

    /// <summary>
    /// Copies the <c>--overlay</c> files next to the marker and adds them to the project's AdditionalFiles.
    /// </summary>
    private static bool CopyOverlays(
        IReadOnlyList<string> overlays,
        string outputPath,
        List<string> additionalFileNames,
        out List<string> overlayFileNames)
    {
        overlayFileNames = [];
        foreach (var overlay in overlays.Select(OverlayCliHelper.FullPath))
        {
            var overlayFileName = Path.GetFileName(overlay);
            if (!CopySpecificationFile(overlay, outputPath, overlayFileName))
            {
                return false;
            }

            overlayFileNames.Add(overlayFileName);
            additionalFileNames.Add(overlayFileName);
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
        var markerFilePath = Path.Combine(outputPath, Constants.MarkerFile.Client);

        try
        {
            var jsonOptions = JsonSerializerOptionsFactory.Create();

            var json = ConfigurationFileJsonHelper.Serialize(config, Constants.JsonSchema.Client, jsonOptions);

            if (File.Exists(markerFilePath))
            {
                var existingContent = File.ReadAllText(markerFilePath);
                if (existingContent.Trim() == json.Trim())
                {
                    AnsiConsole.MarkupLine($"[dim]✓[/] Marker file up to date: {Constants.MarkerFile.Client}");
                    return true;
                }

                File.WriteAllText(markerFilePath, json);
                AnsiConsole.MarkupLine($"[green]✓[/] Updated marker file: {Constants.MarkerFile.Client}");
            }
            else
            {
                File.WriteAllText(markerFilePath, json);
                AnsiConsole.MarkupLine($"[green]✓[/] Created marker file: {Constants.MarkerFile.Client}");
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
        string specFileName,
        IReadOnlyList<string> partFileNames,
        Version sourceGeneratorVersion,
        Version restClientVersion,
        Version? resilienceVersion)
    {
        var csprojPath = Path.Combine(outputPath, $"{projectName}.csproj");

        try
        {
            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateProjectFileContent(specFileName, sourceGeneratorVersion, restClientVersion, resilienceVersion);
                File.WriteAllText(csprojPath, csprojContent);
                foreach (var partFileName in partFileNames)
                {
                    UpdateProjectFileIfNeeded(csprojPath, partFileName);
                }

                AnsiConsole.MarkupLine($"[green]✓[/] Created project file: {projectName}.csproj");
            }
            else
            {
                var updated = UpdateProjectFileIfNeeded(csprojPath, specFileName);
                foreach (var partFileName in partFileNames)
                {
                    updated |= UpdateProjectFileIfNeeded(csprojPath, partFileName);
                }

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

    private static string GenerateProjectFileContent(
        string specFileName,
        Version sourceGeneratorVersion,
        Version restClientVersion,
        Version? resilienceVersion)
    {
        var resilienceRef = resilienceVersion is not null
            ? $"""

                   <PackageReference Include="{PackageVersionDefaults.ResiliencePackageId}" Version="{resilienceVersion}" />
               """
            : string.Empty;

        return $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFramework>{TargetFrameworkResult.RequiredTargetFramework}</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="{PackageVersionDefaults.SourceGeneratorPackageId}" Version="{sourceGeneratorVersion}" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                <PackageReference Include="{PackageVersionDefaults.RestClientPackageId}" Version="{restClientVersion}" />{resilienceRef}
              </ItemGroup>

              <ItemGroup>
                <AdditionalFiles Include="{specFileName}" />
                <AdditionalFiles Include=".atc-rest-api-client" />
              </ItemGroup>

            </Project>
            """;
    }

    /// <summary>
    /// The start of the line holding the <c>&lt;/ItemGroup&gt;</c> of the item group with the <c>AdditionalFiles</c>
    /// entries - inserting there keeps a new entry in that group, at the entries' indentation. Returns -1 when
    /// there is no such group.
    /// </summary>
    private static int AdditionalFilesGroupEndLineStart(string content)
    {
        var entryPosition = content.LastIndexOf("<AdditionalFiles", StringComparison.OrdinalIgnoreCase);
        var tagPosition = entryPosition < 0
            ? -1
            : content.IndexOf("</ItemGroup>", entryPosition, StringComparison.OrdinalIgnoreCase);
        return tagPosition <= 0
            ? -1
            : content.LastIndexOf('\n', tagPosition) + 1;
    }

    private static bool UpdateProjectFileIfNeeded(
        string csprojPath,
        string specFileName)
    {
        var content = File.ReadAllText(csprojPath);
        var originalContent = content;
        var newLine = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        // Check if AdditionalFiles entries exist
        var hasYamlInclude = content.Contains($"Include=\"{specFileName}\"", StringComparison.OrdinalIgnoreCase);
        var hasMarkerInclude = content.Contains(Constants.MarkerFile.Client, StringComparison.OrdinalIgnoreCase);

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
                var insertPosition = AdditionalFilesGroupEndLineStart(content);
                if (insertPosition >= 0)
                {
                    var yamlEntry = $"    <AdditionalFiles Include=\"{specFileName}\" />{newLine}";
                    content = content.Insert(insertPosition, yamlEntry);
                }
            }

            if (!hasMarkerInclude)
            {
                var insertPosition = AdditionalFilesGroupEndLineStart(content);
                if (insertPosition >= 0)
                {
                    var markerEntry = $"    <AdditionalFiles Include=\"{Constants.MarkerFile.Client}\" />{newLine}";
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
                    <AdditionalFiles Include="{Constants.MarkerFile.Client}" />
                  </ItemGroup>

                """;
                content = content.Insert(insertPosition, newItemGroup.ReplaceLineEndings(newLine));
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