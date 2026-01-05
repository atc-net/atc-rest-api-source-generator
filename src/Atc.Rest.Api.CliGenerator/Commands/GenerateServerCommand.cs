namespace Atc.Rest.Api.CliGenerator.Commands;

/// <summary>
/// Command to generate server contracts and optionally domain handler scaffolds from an OpenAPI specification.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class GenerateServerCommand : Command<GenerateServerCommandSettings>
{
    private const string ContractsMarkerFileName = ".atc-rest-api-server-contracts";
    private const string DomainMarkerFileName = ".atc-rest-api-server-handlers";

    private readonly ProjectScaffoldingService scaffoldingService = new();

    public override int Execute(
        CommandContext context,
        GenerateServerCommandSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        WriteHeader();

        var specPath = Path.GetFullPath(settings.SpecificationPath);
        var outputPath = Path.GetFullPath(settings.OutputPath);
        var projectName = settings.ProjectName;

        // Load options from file (auto-discovery or explicit path)
        var options = Helpers.ApiOptionsHelper.LoadOptions(settings.OptionsPath, specPath);

        // Apply CLI argument overrides (scaffolding first, then use it for server/domain)
        var scaffoldingConfig = ApplyScaffoldingCliOverrides(options, settings);
        var serverConfig = ApplyServerCliOverrides(options, settings, scaffoldingConfig);
        var domainConfig = ApplyDomainCliOverrides(options, settings, scaffoldingConfig);

        // Determine project structure modes
        var isTwoProjects = scaffoldingConfig.ProjectStructure == ProjectStructureType.TwoProjects;
        var isThreeProjects = scaffoldingConfig.ProjectStructure == ProjectStructureType.ThreeProjects;
        var hasSeparateDomain = isTwoProjects || isThreeProjects;

        // Always use repo structure
        var srcPath = Path.Combine(outputPath, "src");

        // Determine project names based on structure mode:
        // - SingleProject: {Name}.Api (all-in-one)
        // - TwoProjects: {Name}.Api (host+contracts), {Name}.Domain
        // - ThreeProjects: {Name}.Api (host), {Name}.Api.Contracts, {Name}.Domain
        var baseName = ExtractSolutionName(projectName);

        string contractsProjectName;
        string hostProjectName;
        string domainProjectName;

        if (isThreeProjects)
        {
            // 3 projects: Host, Contracts, Domain
            contractsProjectName = scaffoldingConfig.ContractsProjectName ?? $"{baseName}.Api.Contracts";
            hostProjectName = scaffoldingConfig.HostProjectName ?? $"{baseName}.Api";
            domainProjectName = scaffoldingConfig.DomainProjectName ?? $"{baseName}.Domain";
        }
        else if (isTwoProjects)
        {
            // 2 projects: Host+Contracts combined, Domain separate
            contractsProjectName = scaffoldingConfig.ContractsProjectName ?? $"{baseName}.Api";
            hostProjectName = contractsProjectName; // Same as contracts (combined)
            domainProjectName = scaffoldingConfig.DomainProjectName ?? $"{baseName}.Domain";
        }
        else
        {
            // SingleProject: All in one
            contractsProjectName = scaffoldingConfig.ContractsProjectName ?? $"{baseName}.Api";
            hostProjectName = contractsProjectName;
            domainProjectName = contractsProjectName;
        }

        var contractsOutputPath = Path.Combine(srcPath, contractsProjectName);
        var domainOutputPath = string.IsNullOrWhiteSpace(settings.DomainOutputPath)
            ? Path.Combine(srcPath, domainProjectName)
            : Path.GetFullPath(settings.DomainOutputPath);

        AnsiConsole.MarkupLine($"[blue]Specification:[/] {specPath}");
        AnsiConsole.MarkupLine($"[blue]Project structure:[/] {scaffoldingConfig.ProjectStructure}");
        AnsiConsole.MarkupLine($"[blue]Repository root:[/] {outputPath}");

        if (isThreeProjects)
        {
            AnsiConsole.MarkupLine($"[blue]Host project:[/] {hostProjectName}");
            AnsiConsole.MarkupLine($"[blue]Contracts project:[/] {contractsProjectName}");
            AnsiConsole.MarkupLine($"[blue]Domain project:[/] {domainProjectName}");
        }
        else if (isTwoProjects)
        {
            AnsiConsole.MarkupLine($"[blue]Host+Contracts project:[/] {contractsProjectName}");
            AnsiConsole.MarkupLine($"[blue]Domain project:[/] {domainProjectName}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[blue]Project:[/] {contractsProjectName}");
        }

        AnsiConsole.MarkupLine($"[blue]Validation mode:[/] {serverConfig.ValidateSpecificationStrategy}");
        AnsiConsole.MarkupLine($"[blue]Versioning strategy:[/] {serverConfig.VersioningStrategy}");

        if (!string.IsNullOrWhiteSpace(serverConfig.Namespace))
        {
            AnsiConsole.MarkupLine($"[blue]Namespace:[/] {serverConfig.Namespace}");
        }

        if (scaffoldingConfig.NoCodingRules)
        {
            AnsiConsole.MarkupLine("[blue]Coding rules:[/] disabled");
        }

        AnsiConsole.WriteLine();

        // Track created projects for statistics
        var projectsCreated = new List<string>();
        OpenApiDocument? parsedDocument = null;
        IReadOnlyList<DiagnosticMessage> validationDiagnostics = [];

        var statusResult = AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .Start("Initializing...", ctx =>
            {
                // Step 1: Validate the OpenAPI specification
                ctx.Status("Validating OpenAPI specification...");
                var validationResult = ValidateSpecificationWithStats(specPath, serverConfig.ValidateSpecificationStrategy);
                if (!validationResult.Success)
                {
                    return 1;
                }

                parsedDocument = validationResult.Document;
                validationDiagnostics = validationResult.Diagnostics;

                // Step 2: Initialize repository structure (always)
                ctx.Status("Initializing repository structure...");
                if (!scaffoldingService.CreateDirectoryStructure(outputPath))
                {
                    return 1;
                }

                if (!scaffoldingService.CreateGitIgnore(outputPath))
                {
                    return 1;
                }

                if (!scaffoldingService.CreateDirectoryBuildProps(outputPath, scaffoldingConfig.TargetFramework))
                {
                    return 1;
                }

                // Step 3: Generate contracts project (or combined host+contracts for TwoProjects)
                ctx.Status("Setting up contracts project...");
                if (isTwoProjects)
                {
                    // TwoProjects: Combined host + contracts project
                    if (!GenerateCombinedHostContractsProject(ctx, specPath, contractsOutputPath, contractsProjectName, domainProjectName, serverConfig, scaffoldingConfig.TargetFramework, scaffoldingConfig.HostUi, scaffoldingConfig.HostUiMode))
                    {
                        return 1;
                    }

                    projectsCreated.Add(contractsProjectName);
                }
                else
                {
                    if (!GenerateContractsProject(ctx, specPath, contractsOutputPath, contractsProjectName, serverConfig))
                    {
                        return 1;
                    }

                    projectsCreated.Add(contractsProjectName);
                }

                // Step 4: Generate domain project (TwoProjects and ThreeProjects modes)
                if (hasSeparateDomain)
                {
                    ctx.Status("Setting up domain project...");
                    if (!GenerateDomainProject(ctx, specPath, domainOutputPath, domainProjectName, contractsProjectName, domainConfig))
                    {
                        return 1;
                    }

                    projectsCreated.Add(domainProjectName);
                }

                // Step 5: Generate host project (ThreeProjects mode only - TwoProjects has combined host+contracts)
                if (isThreeProjects)
                {
                    ctx.Status("Setting up API host project...");
                    if (!scaffoldingService.GenerateHostProject(
                        srcPath,
                        hostProjectName,
                        contractsProjectName,
                        domainProjectName,
                        scaffoldingConfig.TargetFramework,
                        scaffoldingConfig.HostUi,
                        scaffoldingConfig.HostUiMode))
                    {
                        return 1;
                    }

                    projectsCreated.Add(hostProjectName);
                }

                // Step 5b: Generate Aspire AppHost project (if --aspire is specified)
                string? aspireProjectName = null;
                if (scaffoldingConfig.Aspire)
                {
                    ctx.Status("Setting up Aspire AppHost project...");
                    aspireProjectName = $"{baseName}.Aspire";
                    var apiProjectName = isThreeProjects || isTwoProjects ? hostProjectName : contractsProjectName;
                    if (!scaffoldingService.GenerateAspireProject(
                        srcPath,
                        aspireProjectName,
                        apiProjectName,
                        scaffoldingConfig.TargetFramework))
                    {
                        return 1;
                    }

                    projectsCreated.Add(aspireProjectName);
                }

                // Step 6: Generate test project
                ctx.Status("Setting up test project...");
                var testPath = Path.Combine(outputPath, "test");
                if (!scaffoldingService.GenerateTestProject(testPath, contractsProjectName, scaffoldingConfig.TargetFramework))
                {
                    return 1;
                }

                // Step 7: Generate solution file
                ctx.Status("Creating solution file...");
                var srcProjects = new List<string>();

                if (isThreeProjects)
                {
                    // Order: Host, Contracts, Domain
                    srcProjects.Add($"src/{hostProjectName}/{hostProjectName}.csproj");
                    srcProjects.Add($"src/{contractsProjectName}/{contractsProjectName}.csproj");
                    srcProjects.Add($"src/{domainProjectName}/{domainProjectName}.csproj");
                }
                else if (isTwoProjects)
                {
                    // Order: Host+Contracts, Domain
                    srcProjects.Add($"src/{contractsProjectName}/{contractsProjectName}.csproj");
                    srcProjects.Add($"src/{domainProjectName}/{domainProjectName}.csproj");
                }
                else
                {
                    srcProjects.Add($"src/{contractsProjectName}/{contractsProjectName}.csproj");
                }

                // Add Aspire project if it was generated
                if (aspireProjectName is not null)
                {
                    srcProjects.Add($"src/{aspireProjectName}/{aspireProjectName}.csproj");
                }

                // Get test project name using same logic as ProjectScaffoldingService
                var testProjectName = ExtractSolutionName(contractsProjectName) + ".Tests";
                var testProjects = new List<string>
                {
                    $"test/{testProjectName}/{testProjectName}.csproj",
                };

                if (!scaffoldingService.GenerateSolutionFile(outputPath, contractsProjectName, srcProjects, testProjects))
                {
                    return 1;
                }

                // Step 8: Add coding rules files (unless --no-coding-rules)
                if (!scaffoldingConfig.NoCodingRules)
                {
                    ctx.Status("Adding coding rules files...");
                    if (!scaffoldingService.AddCodingRulesFiles(outputPath))
                    {
                        return 1;
                    }
                }

                return 0;
            });

        // Output results after Status context is complete (prevents spinner text bleeding)
        if (statusResult != 0)
        {
            return statusResult;
        }

        stopwatch.Stop();
        AnsiConsole.MarkupLine("[green]Server project setup completed successfully.[/]");

        // Output generation report
        if (parsedDocument is not null)
        {
            var specFileName = Path.GetFileName(specPath);
            var stats = StatisticsCollector.CollectFromOpenApiDocument(
                parsedDocument,
                specFileName,
                "Server",
                validationDiagnostics);

            var finalStats = stats with
            {
                Duration = stopwatch.Elapsed,
                ProjectsCreated = projectsCreated,
                ProjectStructure = scaffoldingConfig.ProjectStructure.ToString(),
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

        AnsiConsole.MarkupLine(
            isThreeProjects || isTwoProjects
                ? "[dim]Run 'dotnet build' in the solution to generate all code.[/]"
                : $"[dim]Run 'dotnet build' in {contractsOutputPath} to generate the server code.[/]");

        return 0;
    }

    private static ScaffoldingOptions ApplyScaffoldingCliOverrides(
        ApiGeneratorOptions options,
        GenerateServerCommandSettings settings)
    {
        // Start with config from options file
        var config = new ScaffoldingOptions
        {
            ProjectStructure = options.Scaffolding.ProjectStructure,
            NoCodingRules = options.Scaffolding.NoCodingRules,
            TestFramework = options.Scaffolding.TestFramework,
            TargetFramework = options.Scaffolding.TargetFramework,
            HostUi = options.Scaffolding.HostUi,
            HostUiMode = options.Scaffolding.HostUiMode,
        };

        // Override with CLI flags
        if (settings.NoCodingRules)
        {
            config.NoCodingRules = true;
        }

        if (!string.IsNullOrWhiteSpace(settings.ProjectStructure) &&
            Enum.TryParse<ProjectStructureType>(settings.ProjectStructure, ignoreCase: true, out var projectStructure))
        {
            config.ProjectStructure = projectStructure;
        }

        // Apply project name overrides
        if (!string.IsNullOrWhiteSpace(settings.HostProjectName))
        {
            config.HostProjectName = settings.HostProjectName;
        }

        if (!string.IsNullOrWhiteSpace(settings.ContractsProjectName))
        {
            config.ContractsProjectName = settings.ContractsProjectName;
        }

        if (!string.IsNullOrWhiteSpace(settings.DomainProjectName))
        {
            config.DomainProjectName = settings.DomainProjectName;
        }

        // Apply namespace overrides
        if (!string.IsNullOrWhiteSpace(settings.HostNamespace))
        {
            config.HostNamespace = settings.HostNamespace;
        }

        if (!string.IsNullOrWhiteSpace(settings.ContractsNamespace))
        {
            config.ContractsNamespace = settings.ContractsNamespace;
        }

        if (!string.IsNullOrWhiteSpace(settings.DomainNamespace))
        {
            config.DomainNamespace = settings.DomainNamespace;
        }

        // Apply host UI overrides
        if (!string.IsNullOrWhiteSpace(settings.HostUi) &&
            Enum.TryParse<HostUiType>(settings.HostUi, ignoreCase: true, out var hostUi))
        {
            config.HostUi = hostUi;
        }

        if (!string.IsNullOrWhiteSpace(settings.HostUiMode) &&
            Enum.TryParse<HostUiModeType>(settings.HostUiMode, ignoreCase: true, out var hostUiMode))
        {
            config.HostUiMode = hostUiMode;
        }

        // Apply Aspire override
        if (settings.Aspire)
        {
            config.Aspire = true;
        }

        return config;
    }

    private static ServerConfig ApplyServerCliOverrides(
        ApiGeneratorOptions options,
        GenerateServerCommandSettings settings,
        ScaffoldingOptions scaffoldingConfig)
    {
        // Start with config from options file
        var config = options.ToServerConfig();

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

        // Override namespace: contracts-namespace takes precedence over --namespace
        if (!string.IsNullOrWhiteSpace(scaffoldingConfig.ContractsNamespace))
        {
            config.Namespace = scaffoldingConfig.ContractsNamespace;
        }
        else if (!string.IsNullOrWhiteSpace(settings.Namespace))
        {
            config.Namespace = settings.Namespace;
        }

        // Override sub-folder strategy if specified
        if (!string.IsNullOrWhiteSpace(settings.SubFolderStrategy) &&
            Enum.TryParse<SubFolderStrategyType>(settings.SubFolderStrategy, ignoreCase: true, out var subFolderStrategy))
        {
            config.SubFolderStrategy = subFolderStrategy;
        }

        // Override versioning strategy if specified
        if (!string.IsNullOrWhiteSpace(settings.VersioningStrategy) &&
            Enum.TryParse<VersioningStrategyType>(settings.VersioningStrategy, ignoreCase: true, out var versioningStrategy))
        {
            config.VersioningStrategy = versioningStrategy;
        }

        // Override default API version if specified
        if (!string.IsNullOrWhiteSpace(settings.DefaultApiVersion))
        {
            config.DefaultApiVersion = settings.DefaultApiVersion;
        }

        return config;
    }

    private static ServerDomainConfig ApplyDomainCliOverrides(
        ApiGeneratorOptions options,
        GenerateServerCommandSettings settings,
        ScaffoldingOptions scaffoldingConfig)
    {
        // Start with config from options file
        var config = options.ToServerDomainConfig();

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

        // Override namespace: domain-namespace takes precedence
        if (!string.IsNullOrWhiteSpace(scaffoldingConfig.DomainNamespace))
        {
            config.Namespace = scaffoldingConfig.DomainNamespace;
        }

        // Override handler suffix if specified
        if (!string.IsNullOrWhiteSpace(settings.HandlerSuffix))
        {
            config.HandlerSuffix = settings.HandlerSuffix;
        }

        // Override stub implementation if specified
        if (!string.IsNullOrWhiteSpace(settings.StubImplementation))
        {
            config.StubImplementation = settings.StubImplementation;
        }

        return config;
    }

    private static string ExtractSolutionName(string projectName)
    {
        // Remove common suffixes to get base name
        var name = projectName;
        var suffixes = new[] { ".Api.Contracts", ".Api.Domain", ".Api", ".Contracts", ".Domain" };

        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^suffix.Length];
                break;
            }
        }

        return name;
    }

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Server Project Generator[/]");
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

            var errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (errors.Count > 0)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Validation failed with {errors.Count} error(s):");
                foreach (var error in errors)
                {
                    AnsiConsole.MarkupLine($"  [red][[{error.RuleId}]] {Markup.Escape(error.Message)}[/]");
                }

                return (false, parsedDoc, diagnostics);
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

    private static bool GenerateContractsProject(
        StatusContext ctx,
        string specPath,
        string outputPath,
        string projectName,
        ServerConfig config)
    {
        // Validate output directory
        if (!ValidateOutputDirectory(outputPath, projectName))
        {
            return false;
        }

        // Create output directory if it doesn't exist
        ctx.Status("Creating contracts output directory...");
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
            return false;
        }

        // Copy specification file
        ctx.Status("Copying specification file...");
        var specFileName = Path.GetFileName(specPath);
        if (!CopySpecificationFile(specPath, outputPath, specFileName))
        {
            return false;
        }

        // Create or update marker file with server config
        ctx.Status("Creating contracts marker file...");
        if (!CreateOrUpdateContractsMarkerFile(outputPath, config))
        {
            return false;
        }

        // Create or update project file
        ctx.Status("Creating contracts project file...");
        if (!CreateOrUpdateContractsProjectFile(outputPath, projectName, specFileName))
        {
            return false;
        }

        return true;
    }

    private static bool GenerateDomainProject(
        StatusContext ctx,
        string specPath,
        string outputPath,
        string projectName,
        string contractsProjectName,
        ServerDomainConfig config)
    {
        // Validate output directory
        if (!ValidateOutputDirectory(outputPath, projectName))
        {
            return false;
        }

        // Create output directory if it doesn't exist
        ctx.Status("Creating domain output directory...");
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
            return false;
        }

        // Copy specification file
        ctx.Status("Copying specification file...");
        var specFileName = Path.GetFileName(specPath);
        if (!CopySpecificationFile(specPath, outputPath, specFileName))
        {
            return false;
        }

        // Create or update marker file with domain config
        ctx.Status("Creating domain marker file...");
        if (!CreateOrUpdateDomainMarkerFile(outputPath, config))
        {
            return false;
        }

        // Create or update project file
        ctx.Status("Creating domain project file...");
        if (!CreateOrUpdateDomainProjectFile(outputPath, projectName, contractsProjectName, specFileName))
        {
            return false;
        }

        return true;
    }

    private static bool GenerateCombinedHostContractsProject(
        StatusContext ctx,
        string specPath,
        string outputPath,
        string projectName,
        string domainProjectName,
        ServerConfig config,
        string targetFramework,
        HostUiType hostUi,
        HostUiModeType hostUiMode)
    {
        // Validate output directory
        if (!ValidateOutputDirectory(outputPath, projectName))
        {
            return false;
        }

        // Create output directory if it doesn't exist
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
            return false;
        }

        // Copy specification file
        ctx.Status("Copying specification file...");
        var specFileName = Path.GetFileName(specPath);
        if (!CopySpecificationFile(specPath, outputPath, specFileName))
        {
            return false;
        }

        // Create or update marker file with server config
        ctx.Status("Creating contracts marker file...");
        if (!CreateOrUpdateContractsMarkerFile(outputPath, config))
        {
            return false;
        }

        // Create or update combined host+contracts project file
        ctx.Status("Creating project file...");
        if (!CreateOrUpdateCombinedHostContractsProjectFile(outputPath, projectName, domainProjectName, specFileName, targetFramework, hostUi))
        {
            return false;
        }

        // Create Program.cs
        ctx.Status("Creating Program.cs...");
        if (!CreateOrUpdateProgramFile(outputPath, projectName, hostUi, hostUiMode))
        {
            return false;
        }

        return true;
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
                var sourceLastWrite = File.GetLastWriteTimeUtc(sourcePath);
                var destLastWrite = File.GetLastWriteTimeUtc(destinationPath);

                if (sourceLastWrite <= destLastWrite)
                {
                    AnsiConsole.MarkupLine($"[dim]✓[/] Specification file up to date: {fileName}");
                    return true;
                }

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

    private static bool CreateOrUpdateContractsMarkerFile(
        string outputPath,
        ServerConfig config)
    {
        var markerFilePath = Path.Combine(outputPath, ContractsMarkerFileName);

        try
        {
            var jsonOptions = JsonSerializerOptionsFactory.Create();

            var json = JsonSerializer.Serialize(config, jsonOptions);

            if (File.Exists(markerFilePath))
            {
                var existingContent = File.ReadAllText(markerFilePath);
                if (existingContent.Trim() == json.Trim())
                {
                    AnsiConsole.MarkupLine($"[dim]✓[/] Marker file up to date: {ContractsMarkerFileName}");
                    return true;
                }

                File.WriteAllText(markerFilePath, json);
                AnsiConsole.MarkupLine($"[green]✓[/] Updated marker file: {ContractsMarkerFileName}");
            }
            else
            {
                File.WriteAllText(markerFilePath, json);
                AnsiConsole.MarkupLine($"[green]✓[/] Created marker file: {ContractsMarkerFileName}");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error creating marker file: {ex.Message}");
            return false;
        }
    }

    private static bool CreateOrUpdateDomainMarkerFile(
        string outputPath,
        ServerDomainConfig config)
    {
        var markerFilePath = Path.Combine(outputPath, DomainMarkerFileName);

        try
        {
            var jsonOptions = JsonSerializerOptionsFactory.Create();

            var json = JsonSerializer.Serialize(config, jsonOptions);

            if (File.Exists(markerFilePath))
            {
                var existingContent = File.ReadAllText(markerFilePath);
                if (existingContent.Trim() == json.Trim())
                {
                    AnsiConsole.MarkupLine($"[dim]✓[/] Marker file up to date: {DomainMarkerFileName}");
                    return true;
                }

                File.WriteAllText(markerFilePath, json);
                AnsiConsole.MarkupLine($"[green]✓[/] Updated marker file: {DomainMarkerFileName}");
            }
            else
            {
                File.WriteAllText(markerFilePath, json);
                AnsiConsole.MarkupLine($"[green]✓[/] Created marker file: {DomainMarkerFileName}");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error creating marker file: {ex.Message}");
            return false;
        }
    }

    private static bool CreateOrUpdateContractsProjectFile(
        string outputPath,
        string projectName,
        string specFileName)
    {
        var csprojPath = Path.Combine(outputPath, $"{projectName}.csproj");

        try
        {
            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateContractsProjectFileContent(specFileName);
                File.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLine($"[green]✓[/] Created project file: {projectName}.csproj");
            }
            else
            {
                var updated = UpdateProjectFileIfNeeded(csprojPath, specFileName, ContractsMarkerFileName);
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

    private static bool CreateOrUpdateDomainProjectFile(
        string outputPath,
        string projectName,
        string contractsProjectName,
        string specFileName)
    {
        var csprojPath = Path.Combine(outputPath, $"{projectName}.csproj");

        try
        {
            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateDomainProjectFileContent(specFileName, contractsProjectName);
                File.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLine($"[green]✓[/] Created project file: {projectName}.csproj");
            }
            else
            {
                var updated = UpdateProjectFileIfNeeded(csprojPath, specFileName, DomainMarkerFileName);
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

    private static string GenerateContractsProjectFileContent(
        string specFileName)
        => $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>

              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>

              <ItemGroup>
                <PackageReference Include="Atc.Rest.Api.SourceGenerator" Version="*" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                <PackageReference Include="Microsoft.Extensions.Caching.Hybrid" Version="10.1.0" />
              </ItemGroup>

              <ItemGroup>
                <AdditionalFiles Include="{specFileName}" />
                <AdditionalFiles Include="{ContractsMarkerFileName}" />
              </ItemGroup>

            </Project>
            """;

    private static string GenerateDomainProjectFileContent(
        string specFileName,
        string contractsProjectName)
        => $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>

              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>

              <ItemGroup>
                <ProjectReference Include="..\{contractsProjectName}\{contractsProjectName}.csproj" />
              </ItemGroup>

              <ItemGroup>
                <PackageReference Include="Atc.Rest.Api.SourceGenerator" Version="*" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>

              <ItemGroup>
                <AdditionalFiles Include="{specFileName}" />
                <AdditionalFiles Include="{DomainMarkerFileName}" />
              </ItemGroup>

            </Project>
            """;

    private static bool CreateOrUpdateCombinedHostContractsProjectFile(
        string outputPath,
        string projectName,
        string domainProjectName,
        string specFileName,
        string targetFramework,
        HostUiType hostUi)
    {
        var csprojPath = Path.Combine(outputPath, $"{projectName}.csproj");

        try
        {
            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateCombinedHostContractsProjectFileContent(specFileName, domainProjectName, targetFramework, hostUi);
                File.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLine($"[green]✓[/] Created project file: {projectName}.csproj");
            }
            else
            {
                var updated = UpdateProjectFileIfNeeded(csprojPath, specFileName, ContractsMarkerFileName);
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

    private static string GenerateCombinedHostContractsProjectFileContent(
        string specFileName,
        string domainProjectName,
        string targetFramework,
        HostUiType hostUi)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk.Web\">");
        sb.AppendLine();
        sb.AppendLine(2, "<PropertyGroup>");
        sb.AppendLine(4, $"<TargetFramework>{targetFramework}</TargetFramework>");
        sb.AppendLine(2, "</PropertyGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");
        sb.AppendLine(4, $"<ProjectReference Include=\"..\\{domainProjectName}\\{domainProjectName}.csproj\" />");
        sb.AppendLine(2, "</ItemGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");
        sb.AppendLine(4, "<PackageReference Include=\"Atc.Rest.Api.SourceGenerator\" Version=\"*\" OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\" />");
        sb.AppendLine(4, "<PackageReference Include=\"Microsoft.Extensions.Caching.Hybrid\" Version=\"10.1.0\" />");

        if (hostUi != HostUiType.None)
        {
            sb.AppendLine(4, "<PackageReference Include=\"Microsoft.AspNetCore.OpenApi\" Version=\"10.*\" />");
        }

        switch (hostUi)
        {
            case HostUiType.Scalar:
                sb.AppendLine(4, "<PackageReference Include=\"Scalar.AspNetCore\" Version=\"2.*\" />");
                break;
            case HostUiType.Swagger:
                sb.AppendLine(4, "<PackageReference Include=\"Swashbuckle.AspNetCore.SwaggerUI\" Version=\"7.*\" />");
                break;
        }

        sb.AppendLine(2, "</ItemGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");
        sb.AppendLine(4, $"<AdditionalFiles Include=\"{specFileName}\" />");
        sb.AppendLine(4, $"<AdditionalFiles Include=\"{ContractsMarkerFileName}\" />");
        sb.AppendLine(2, "</ItemGroup>");
        sb.AppendLine();
        sb.Append("</Project>");

        return sb.ToString();
    }

    private static bool CreateOrUpdateProgramFile(
        string outputPath,
        string projectName,
        HostUiType hostUi,
        HostUiModeType hostUiMode)
    {
        var programPath = Path.Combine(outputPath, "Program.cs");

        try
        {
            if (!File.Exists(programPath))
            {
                var baseName = ExtractSolutionName(projectName);
                var programContent = GenerateProgramContent(baseName, hostUi, hostUiMode);
                File.WriteAllText(programPath, programContent);
                AnsiConsole.MarkupLine("[green]✓[/] Created Program.cs");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]✓[/] Program.cs already exists");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error creating Program.cs: {ex.Message}");
            return false;
        }
    }

    private static string GenerateProgramContent(
        string baseName,
        HostUiType hostUi,
        HostUiModeType hostUiMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();

        // Add OpenAPI services if UI is enabled
        if (hostUi != HostUiType.None)
        {
            sb.AppendLine("// Configure OpenAPI document generation");
            sb.AppendLine("builder.Services.AddOpenApi();");
            sb.AppendLine();
        }

        sb.AppendLine("// Add API services (rate limiting, security, etc. from OpenAPI spec)");
        sb.Append("builder.Services.Add");
        sb.Append(baseName);
        sb.AppendLine("Api();");
        sb.AppendLine();
        sb.AppendLine("// Register handler implementations and validators from Domain project");
        sb.AppendLine("builder.Services.AddApiHandlersFromDomain();");
        sb.AppendLine("builder.Services.AddApiValidatorsFromDomain();");
        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();

        // Generate UI mapping based on configuration
        if (hostUi != HostUiType.None)
        {
            if (hostUiMode == HostUiModeType.DevelopmentOnly)
            {
                sb.AppendLine("if (app.Environment.IsDevelopment())");
                sb.AppendLine("{");
                sb.AppendLine(4, "app.MapOpenApi();");
                AppendUiMapping(sb, hostUi, 4);
                sb.AppendLine("}");
            }
            else
            {
                sb.AppendLine("app.MapOpenApi();");
                AppendUiMapping(sb, hostUi, 0);
            }

            sb.AppendLine();

            // Add root redirect to UI
            var redirectPath = hostUi == HostUiType.Swagger ? "/swagger" : "/scalar/v1";
            sb.AppendLine("// Redirect root to API documentation");
            sb.Append("app.MapGet(\"/\", () => Results.Redirect(\"");
            sb.Append(redirectPath);
            sb.AppendLine("\")).ExcludeFromDescription();");
            sb.AppendLine();
        }

        sb.AppendLine("// Configure middleware and map all endpoints");
        sb.Append("app.Map");
        sb.Append(baseName);
        sb.AppendLine("Api();");
        sb.AppendLine();
        sb.Append("app.Run();");

        return sb.ToString();
    }

    private static void AppendUiMapping(
        StringBuilder sb,
        HostUiType hostUi,
        int indent)
    {
        switch (hostUi)
        {
            case HostUiType.Scalar:
                sb.AppendLine(indent, "app.MapScalarApiReference();");
                break;
            case HostUiType.Swagger:
                sb.AppendLine(indent, "app.UseSwaggerUI(options =>");
                sb.AppendLine(indent, "{");
                sb.AppendLine(indent + 4, "options.SwaggerEndpoint(\"/openapi/v1.json\", \"v1\");");
                sb.AppendLine(indent, "});");
                break;
        }
    }

    private static bool UpdateProjectFileIfNeeded(
        string csprojPath,
        string specFileName,
        string markerFileName)
    {
        var content = File.ReadAllText(csprojPath);
        var originalContent = content;

        var hasYamlInclude = content.Contains($"Include=\"{specFileName}\"", StringComparison.OrdinalIgnoreCase);
        var hasMarkerInclude = content.Contains(markerFileName, StringComparison.OrdinalIgnoreCase);

        if (hasYamlInclude && hasMarkerInclude)
        {
            return false;
        }

        if (content.Contains("<AdditionalFiles", StringComparison.OrdinalIgnoreCase))
        {
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
                    var markerEntry = $"    <AdditionalFiles Include=\"{markerFileName}\" />\n";
                    content = content.Insert(insertPosition, markerEntry);
                }
            }
        }
        else
        {
            var insertPosition = content.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
            if (insertPosition >= 0)
            {
                var newItemGroup = $"""

                  <ItemGroup>
                    <AdditionalFiles Include="{specFileName}" />
                    <AdditionalFiles Include="{markerFileName}" />
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