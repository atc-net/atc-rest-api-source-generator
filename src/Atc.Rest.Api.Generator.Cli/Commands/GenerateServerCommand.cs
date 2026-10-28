namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to generate server contracts and optionally domain handler scaffolds from an OpenAPI specification.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class GenerateServerCommand : Command<GenerateServerCommandSettings>
{
    private readonly ProjectScaffoldingService scaffoldingService = new();
    private readonly INugetPackageVersionService nugetService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateServerCommand"/> class.
    /// </summary>
    /// <param name="nugetService">Resolves the latest package versions written into the generated project files.</param>
    public GenerateServerCommand(INugetPackageVersionService nugetService)
    {
        this.nugetService = nugetService;
    }

    /// <summary>
    /// Validates the specification, scaffolds the server projects and generates contracts, handlers and host code into them.
    /// </summary>
    /// <param name="context">The Spectre.Console command context.</param>
    /// <param name="settings">The parsed command-line settings.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>0 when generation succeeded; otherwise 1.</returns>
    public override int Execute(
        CommandContext context,
        GenerateServerCommandSettings settings,
        CancellationToken cancellationToken)
        => FileTransaction.Run(() => ExecuteCore(context, settings, cancellationToken));

    private int ExecuteCore(
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

        // Load options from file (auto-discovery or explicit path); a file that cannot be read stops the command
        if (!ApiOptionsHelper.TryLoadOptions(settings.OptionsPath, specPath, out var options))
        {
            return 1;
        }

        // Apply CLI argument overrides (scaffolding first, then use it for server/domain)
        var scaffoldingConfig = ApplyScaffoldingCliOverrides(options, settings);
        var serverConfig = ApplyServerCliOverrides(options, settings, scaffoldingConfig);
        var domainConfig = ApplyDomainCliOverrides(options, settings, scaffoldingConfig);

        // The projects, their names and folders by the project structure
        var plan = ServerScaffoldPlan.Create(projectName, outputPath, settings.DomainOutputPath, scaffoldingConfig);
        var isTwoProjects = plan.IsTwoProjects;
        var isThreeProjects = plan.IsThreeProjects;
        var srcPath = plan.SourcePath;
        var contractsProjectName = plan.ContractsProjectName;
        var hostProjectName = plan.HostProjectName;
        var domainProjectName = plan.DomainProjectName;
        var contractsOutputPath = plan.ContractsOutputPath;
        var domainOutputPath = plan.DomainOutputPath;

        WritePlan(plan, specPath, serverConfig, scaffoldingConfig);

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
                // Step 0: Resolve package version from NuGet (with fallback)
                ctx.Status("Resolving package versions...");
                var sourceGeneratorVersion = TaskHelper.RunSync(() => nugetService.GetLatestVersionOrFallbackAsync(
                    PackageVersionDefaults.SourceGeneratorPackageId,
                    PackageVersionDefaults.SourceGeneratorFallback,
                    cancellationToken));

                // Step 1: Validate the OpenAPI specification
                ctx.Status("Validating OpenAPI specification...");
                var validationResult = SpecificationValidationHelper.ValidateSpecificationWithStats(specPath, serverConfig.ValidateSpecificationStrategy, OverlayCliHelper.All(options.General.Overlays, settings.Overlays), parametersMustBind: true);
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

                // Note: Directory.Build.props is created by atc-coding-rules-updater.ps1 script

                // Step 3: Generate contracts project (TwoProjects: the domain project, which holds the contracts too)
                ctx.Status("Setting up contracts project...");
                if (!GenerateContractsProject(ctx, specPath, contractsOutputPath, contractsProjectName, outputPath, serverConfig, sourceGeneratorVersion))
                {
                    return 1;
                }

                if (!isTwoProjects)
                {
                    projectsCreated.Add(contractsProjectName);
                }

                // Step 4: Generate domain project (TwoProjects and ThreeProjects modes; for TwoProjects it adds the
                // handlers marker to the project step 3 wrote, which references no other project)
                if (plan.HasSeparateDomain)
                {
                    ctx.Status("Setting up domain project...");
                    if (!GenerateDomainProject(ctx, specPath, domainOutputPath, domainProjectName, contractsProjectName, outputPath, domainConfig, sourceGeneratorVersion))
                    {
                        return 1;
                    }

                    projectsCreated.Add(domainProjectName);
                }

                // Step 5: Generate host project (TwoProjects and ThreeProjects modes)
                if (plan.HasSeparateDomain)
                {
                    ctx.Status("Setting up API host project...");
                    if (!scaffoldingService.GenerateHostProject(
                        srcPath,
                        hostProjectName,
                        contractsProjectName,
                        domainProjectName,
                        scaffoldingConfig.TargetFramework,
                        scaffoldingConfig.HostUi,
                        scaffoldingConfig.HostUiMode,
                        scaffoldingConfig.HostNamespace))
                    {
                        return 1;
                    }

                    projectsCreated.Add(hostProjectName);
                }

                // Step 5b: Generate Aspire AppHost project (if --aspire is specified)
                if (plan.AspireProjectName is { } aspireProjectName)
                {
                    ctx.Status("Setting up Aspire AppHost project...");
                    if (!scaffoldingService.GenerateAspireProject(
                        srcPath,
                        aspireProjectName,
                        plan.ApiProjectName,
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
                if (!scaffoldingService.GenerateSolutionFile(outputPath, contractsProjectName, [.. plan.SourceProjects], [.. plan.TestProjects]))
                {
                    return 1;
                }

                // Step 8: Add coding rules files and run updater (unless --no-coding-rules)
                if (!scaffoldingConfig.NoCodingRules)
                {
                    ctx.Status("Adding coding rules files...");
                    if (!scaffoldingService.AddCodingRulesFiles(outputPath))
                    {
                        return 1;
                    }

                    ctx.Status("Running coding rules updater...");
                    scaffoldingService.RunCodingRulesUpdater(
                        outputPath,
                        organizationName: plan.BaseName,
                        repositoryName: plan.BaseName);
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

            ScaffoldCommandSupport.WriteGenerationReport(finalStats, settings.GenerateReport, outputPath);
        }

        AnsiConsole.MarkupLine(
            isThreeProjects || isTwoProjects
                ? "[dim]Run 'dotnet build' in the solution to generate all code.[/]"
                : $"[dim]Run 'dotnet build' in {contractsOutputPath} to generate the server code.[/]");

        return 0;
    }

    // What the command is about to do: the specification, the projects and the settings that shape them
    private static void WritePlan(
        ServerScaffoldPlan plan,
        string specPath,
        ServerConfig serverConfig,
        ScaffoldingOptions scaffoldingConfig)
    {
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Specification:[/] {specPath}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Project structure:[/] {plan.Structure}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Repository root:[/] {plan.RepositoryRoot}");

        if (plan.IsThreeProjects)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Host project:[/] {plan.HostProjectName}");
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Contracts project:[/] {plan.ContractsProjectName}");
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Domain project:[/] {plan.DomainProjectName}");
        }
        else if (plan.IsTwoProjects)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Host project:[/] {plan.HostProjectName}");
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Contracts+Domain project:[/] {plan.DomainProjectName}");
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Project:[/] {plan.ContractsProjectName}");
        }

        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Validation mode:[/] {serverConfig.ValidateSpecificationStrategy}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Versioning strategy:[/] {serverConfig.VersioningStrategy}");

        if (!string.IsNullOrWhiteSpace(serverConfig.Namespace))
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Namespace:[/] {serverConfig.Namespace}");
        }

        if (scaffoldingConfig.NoCodingRules)
        {
            AnsiConsole.MarkupLine("[blue]Coding rules:[/] disabled");
        }

        AnsiConsole.WriteLine();
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
            TargetFramework = options.Scaffolding.TargetFramework,
            HostProjectName = options.Scaffolding.HostProjectName,
            ContractsProjectName = options.Scaffolding.ContractsProjectName,
            DomainProjectName = options.Scaffolding.DomainProjectName,
            HostNamespace = options.Scaffolding.HostNamespace,
            ContractsNamespace = options.Scaffolding.ContractsNamespace,
            DomainNamespace = options.Scaffolding.DomainNamespace,
            HostUi = options.Scaffolding.HostUi,
            HostUiMode = options.Scaffolding.HostUiMode,
            Aspire = options.Scaffolding.Aspire,
        };

        // Override with CLI flags
        if (settings.NoCodingRules)
        {
            config.NoCodingRules = true;
        }

        if (!string.IsNullOrWhiteSpace(settings.ProjectStructure) &&
            EnumOptionHelper.TryParse<ProjectStructureType>(settings.ProjectStructure, out var projectStructure))
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
            EnumOptionHelper.TryParse<HostUiType>(settings.HostUi, out var hostUi))
        {
            config.HostUi = hostUi;
        }

        if (!string.IsNullOrWhiteSpace(settings.HostUiMode) &&
            EnumOptionHelper.TryParse<HostUiModeType>(settings.HostUiMode, out var hostUiMode))
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

        // The --overlay files (placed next to the specification and listed relative to the marker later)
        config.Overlays = OverlayCliHelper.All(options.General.Overlays, settings.Overlays) is { Length: > 0 } overlays ? [.. overlays] : null;

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

        // Namespace: the contracts namespace, then --namespace, then server.namespace from the options file
        if (!string.IsNullOrWhiteSpace(scaffoldingConfig.ContractsNamespace))
        {
            config.Namespace = scaffoldingConfig.ContractsNamespace;
        }
        else if (!string.IsNullOrWhiteSpace(settings.Namespace))
        {
            config.Namespace = settings.Namespace;
        }
        else if (string.IsNullOrWhiteSpace(config.Namespace))
        {
            config.Namespace = ProjectNameHelper.ExtractSolutionName(settings.ProjectName);
        }

        // Override sub-folder strategy if specified
        if (!string.IsNullOrWhiteSpace(settings.SubFolderStrategy) &&
            EnumOptionHelper.TryParse<SubFolderStrategyType>(settings.SubFolderStrategy, out var subFolderStrategy))
        {
            config.SubFolderStrategy = subFolderStrategy;
        }

        // Override versioning strategy if specified
        if (!string.IsNullOrWhiteSpace(settings.VersioningStrategy) &&
            EnumOptionHelper.TryParse<VersioningStrategyType>(settings.VersioningStrategy, out var versioningStrategy))
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

        // The --overlay files (placed next to the specification and listed relative to the marker later)
        config.Overlays = OverlayCliHelper.All(options.General.Overlays, settings.Overlays) is { Length: > 0 } overlays ? [.. overlays] : null;

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

        // Namespace: the domain namespace, then --namespace, then server.namespace from the options file
        if (!string.IsNullOrWhiteSpace(scaffoldingConfig.DomainNamespace))
        {
            config.Namespace = scaffoldingConfig.DomainNamespace;
        }
        else if (!string.IsNullOrWhiteSpace(settings.Namespace))
        {
            config.Namespace = settings.Namespace;
        }
        else if (!string.IsNullOrWhiteSpace(options.Server.Namespace))
        {
            config.Namespace = options.Server.Namespace;
        }
        else
        {
            config.Namespace = ProjectNameHelper.ExtractSolutionName(settings.ProjectName);
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

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Server Project Generator[/]");
        AnsiConsole.WriteLine();
    }

    private static bool GenerateContractsProject(
        StatusContext ctx,
        string specPath,
        string outputPath,
        string projectName,
        string repositoryRoot,
        ServerConfig config,
        Version sourceGeneratorVersion)
    {
        // Validate output directory
        if (!ScaffoldCommandSupport.ValidateOutputDirectory(outputPath, projectName))
        {
            return false;
        }

        // Create output directory if it doesn't exist
        ctx.Status("Creating contracts output directory...");
        try
        {
            if (!Directory.Exists(outputPath))
            {
                TrackedFile.CreateDirectory(outputPath);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created output directory: {outputPath}");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating output directory: {ex.Message}");
            return false;
        }

        // Determine spec file strategy - no duplication!
        ctx.Status("Configuring specification files...");
        var specFiles = GetSpecificationFiles(specPath);
        var (isInsideRepo, specBasePath) = DetermineSpecFileStrategy(specPath, repositoryRoot);

        if (!isInsideRepo)
        {
            // Copy all specs to ScriptsAndSpecifications folder
            if (!EnsureSpecsInRepository(specFiles, repositoryRoot))
            {
                return false;
            }
        }
        else
        {
            var specFileName = Path.GetFileName(specPath);
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]✓[/] Using existing specification: {specFileName}");
        }

        // Build list of relative spec paths for the project file
        var relativeSpecPaths = specFiles
            .Select(f => GetRelativeSpecPath(specBasePath, outputPath, Path.GetFileName(f)))
            .ToList();

        // The overlays go next to the specification and are listed in the marker, relative to it: the build applies them
        if (PlaceOverlays(config.Overlays, specBasePath, outputPath) is not { } relativeOverlayPaths)
        {
            return false;
        }

        relativeSpecPaths.AddRange(relativeOverlayPaths);
        config.Overlays = relativeOverlayPaths.Count > 0 ? relativeOverlayPaths : null;

        // Create or update marker file with server config
        ctx.Status("Creating contracts marker file...");
        if (!CreateOrUpdateContractsMarkerFile(outputPath, config))
        {
            return false;
        }

        // Create or update project file with relative paths
        ctx.Status("Creating contracts project file...");
        if (!CreateOrUpdateContractsProjectFile(outputPath, projectName, relativeSpecPaths, sourceGeneratorVersion))
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
        string repositoryRoot,
        ServerDomainConfig config,
        Version sourceGeneratorVersion)
    {
        // Validate output directory
        if (!ScaffoldCommandSupport.ValidateOutputDirectory(outputPath, projectName))
        {
            return false;
        }

        // Create output directory if it doesn't exist
        ctx.Status("Creating domain output directory...");
        try
        {
            if (!Directory.Exists(outputPath))
            {
                TrackedFile.CreateDirectory(outputPath);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created output directory: {outputPath}");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating output directory: {ex.Message}");
            return false;
        }

        // Determine spec file strategy - no duplication!
        ctx.Status("Configuring specification files...");
        var specFiles = GetSpecificationFiles(specPath);
        var (isInsideRepo, specBasePath) = DetermineSpecFileStrategy(specPath, repositoryRoot);

        if (!isInsideRepo)
        {
            // Copy all specs to ScriptsAndSpecifications folder (if not already done by contracts)
            if (!EnsureSpecsInRepository(specFiles, repositoryRoot))
            {
                return false;
            }
        }
        else
        {
            var specFileName = Path.GetFileName(specPath);
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]✓[/] Using existing specification: {specFileName}");
        }

        // Build list of relative spec paths for the project file
        var relativeSpecPaths = specFiles
            .Select(f => GetRelativeSpecPath(specBasePath, outputPath, Path.GetFileName(f)))
            .ToList();

        // The overlays go next to the specification and are listed in the marker, relative to it: the build applies them
        if (PlaceOverlays(config.Overlays, specBasePath, outputPath) is not { } relativeOverlayPaths)
        {
            return false;
        }

        relativeSpecPaths.AddRange(relativeOverlayPaths);
        config.Overlays = relativeOverlayPaths.Count > 0 ? relativeOverlayPaths : null;

        // Create or update marker file with domain config
        ctx.Status("Creating domain marker file...");
        if (!CreateOrUpdateDomainMarkerFile(outputPath, config))
        {
            return false;
        }

        // Create or update project file with relative paths
        ctx.Status("Creating domain project file...");
        if (!CreateOrUpdateDomainProjectFile(outputPath, projectName, contractsProjectName, relativeSpecPaths, sourceGeneratorVersion))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// The specification files: the base file, then its parts as the build finds them (any specification extension).
    /// </summary>
    private static List<string> GetSpecificationFiles(string primarySpecPath)
        => [primarySpecPath, .. SpecificationService.DiscoverPartFilePaths(primarySpecPath)];

    /// <summary>
    /// Determines if spec is inside repository and returns the effective spec path for projects.
    /// </summary>
    private static (bool IsInsideRepo, string SpecBasePath) DetermineSpecFileStrategy(
        string specPath,
        string repositoryRoot)
    {
        var normalizedSpec = Path.GetFullPath(specPath);
        var normalizedRoot = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        // Check if spec file is inside the repository root
        if (normalizedSpec.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            // Spec is inside repo - use the directory where the spec lives
            return (true, Path.GetDirectoryName(normalizedSpec)!);
        }

        // Spec is outside repo - will use ScriptsAndSpecifications folder
        var specsFolder = Path.Combine(repositoryRoot, "ScriptsAndSpecifications");
        return (false, specsFolder);
    }

    /// <summary>
    /// Ensures all spec files are copied to the ScriptsAndSpecifications folder.
    /// </summary>
    private static bool EnsureSpecsInRepository(
        List<string> specFiles,
        string repositoryRoot)
    {
        var specsFolder = Path.Combine(repositoryRoot, "ScriptsAndSpecifications");

        try
        {
            // Create folder if needed
            if (!Directory.Exists(specsFolder))
            {
                TrackedFile.CreateDirectory(specsFolder);
                AnsiConsole.MarkupLine("[green]✓[/] Created directory: ScriptsAndSpecifications/");
            }

            foreach (var sourcePath in specFiles)
            {
                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(specsFolder, fileName);

                if (!File.Exists(destPath) ||
                    File.GetLastWriteTimeUtc(sourcePath) > File.GetLastWriteTimeUtc(destPath))
                {
                    TrackedFile.Copy(sourcePath, destPath, overwrite: true);
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Copied specification file to ScriptsAndSpecifications/{fileName}");
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]✓[/] Specification file up to date: ScriptsAndSpecifications/{fileName}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error copying specification files: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the relative path from a project directory to the spec files base directory.
    /// </summary>
    /// <summary>
    /// Copies the <c>--overlay</c> files next to the specification (where they are not already) and returns their paths
    /// relative to the project, for the marker and the AdditionalFiles.
    /// </summary>
    private static List<string>? PlaceOverlays(
        IReadOnlyList<string>? overlays,
        string specBasePath,
        string projectDirectory)
    {
        var relativePaths = new List<string>();
        foreach (var overlay in overlays ?? [])
        {
            var source = Path.GetFullPath(overlay);
            var fileName = Path.GetFileName(source);
            var destination = Path.Combine(specBasePath, fileName);
            try
            {
                if (!string.Equals(source, Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                {
                    TrackedFile.CreateDirectory(specBasePath);
                    TrackedFile.Copy(source, destination, overwrite: true);
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Copied overlay next to the specification: {fileName}");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error copying overlay {fileName}: {ex.Message}");
                return null;
            }

            relativePaths.Add(GetRelativeSpecPath(specBasePath, projectDirectory, fileName));
        }

        return relativePaths;
    }

    private static string GetRelativeSpecPath(
        string specBasePath,
        string projectDirectory,
        string specFileName)
    {
        var specFullPath = Path.Combine(specBasePath, specFileName);
        var specUri = new Uri(specFullPath);
        var projectUri = new Uri(projectDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var relativeUri = projectUri.MakeRelativeUri(specUri);
        var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        // Convert URI format to file system path
        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }

    private static bool CreateOrUpdateContractsMarkerFile(
        string outputPath,
        ServerConfig config)
    {
        var markerFilePath = Path.Combine(outputPath, Constants.MarkerFile.Server);

        try
        {
            var jsonOptions = JsonSerializerOptionsFactory.Create();

            var json = ConfigurationFileJsonHelper.Serialize(config, Constants.JsonSchema.Server, jsonOptions);

            if (File.Exists(markerFilePath))
            {
                var existingContent = File.ReadAllText(markerFilePath);
                if (existingContent.Trim() == json.Trim())
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]✓[/] Marker file up to date: {Constants.MarkerFile.Server}");
                    return true;
                }

                // The marker file is the user's once it exists: an edit in it is not rewritten from the options
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] Marker file kept as it is (it differs from the options given): {Constants.MarkerFile.Server}");
            }
            else
            {
                TrackedFile.WriteAllText(markerFilePath, json);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created marker file: {Constants.MarkerFile.Server}");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating marker file: {ex.Message}");
            return false;
        }
    }

    private static bool CreateOrUpdateDomainMarkerFile(
        string outputPath,
        ServerDomainConfig config)
    {
        var markerFilePath = Path.Combine(outputPath, Constants.MarkerFile.ServerHandlers);

        try
        {
            var jsonOptions = JsonSerializerOptionsFactory.Create();

            var json = ConfigurationFileJsonHelper.Serialize(config, Constants.JsonSchema.ServerHandlers, jsonOptions);

            if (File.Exists(markerFilePath))
            {
                var existingContent = File.ReadAllText(markerFilePath);
                if (existingContent.Trim() == json.Trim())
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]✓[/] Marker file up to date: {Constants.MarkerFile.ServerHandlers}");
                    return true;
                }

                // The marker file is the user's once it exists: an edit in it is not rewritten from the options
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] Marker file kept as it is (it differs from the options given): {Constants.MarkerFile.ServerHandlers}");
            }
            else
            {
                TrackedFile.WriteAllText(markerFilePath, json);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created marker file: {Constants.MarkerFile.ServerHandlers}");
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating marker file: {ex.Message}");
            return false;
        }
    }

    private static bool CreateOrUpdateContractsProjectFile(
        string outputPath,
        string projectName,
        List<string> specFilePaths,
        Version sourceGeneratorVersion)
    {
        var csprojPath = Path.Combine(outputPath, $"{projectName}.csproj");

        try
        {
            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateContractsProjectFileContent(specFilePaths, sourceGeneratorVersion);
                TrackedFile.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created project file: {projectName}.csproj");
            }
            else
            {
                var updated = UpdateProjectFileIfNeeded(csprojPath, specFilePaths, Constants.MarkerFile.Server);
                if (updated)
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Updated project file: {projectName}.csproj");
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]✓[/] Project file up to date: {projectName}.csproj");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating/updating project file: {ex.Message}");
            return false;
        }
    }

    private static bool CreateOrUpdateDomainProjectFile(
        string outputPath,
        string projectName,
        string contractsProjectName,
        List<string> specFilePaths,
        Version sourceGeneratorVersion)
    {
        var csprojPath = Path.Combine(outputPath, $"{projectName}.csproj");

        try
        {
            if (!File.Exists(csprojPath))
            {
                var csprojContent = GenerateDomainProjectFileContent(specFilePaths, contractsProjectName, sourceGeneratorVersion);
                TrackedFile.WriteAllText(csprojPath, csprojContent);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Created project file: {projectName}.csproj");
            }
            else
            {
                var updated = UpdateProjectFileIfNeeded(csprojPath, specFilePaths, Constants.MarkerFile.ServerHandlers);
                if (updated)
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Updated project file: {projectName}.csproj");
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[dim]✓[/] Project file up to date: {projectName}.csproj");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error creating/updating project file: {ex.Message}");
            return false;
        }
    }

    private static string GenerateContractsProjectFileContent(
        List<string> specFilePaths,
        Version sourceGeneratorVersion)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        sb.AppendLine();
        sb.AppendLine(2, "<PropertyGroup>");
        sb.AppendLine(4, $"<TargetFramework>{TargetFrameworkResult.RequiredTargetFramework}</TargetFramework>");
        sb.AppendLine(2, "</PropertyGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");
        sb.AppendLine(4, "<FrameworkReference Include=\"Microsoft.AspNetCore.App\" />");
        sb.AppendLine(2, "</ItemGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");
        sb.AppendLine(4, $"<PackageReference Include=\"Atc.Rest.Api.SourceGenerator\" Version=\"{sourceGeneratorVersion}\" OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\" />");
        sb.AppendLine(4, "<PackageReference Include=\"Microsoft.Extensions.Caching.Hybrid\" Version=\"10.3.0\" />");
        sb.AppendLine(2, "</ItemGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");

        foreach (var specPath in specFilePaths)
        {
            sb.AppendLine(4, $"<AdditionalFiles Include=\"{specPath}\" />");
        }

        sb.AppendLine(4, $"<AdditionalFiles Include=\"{Constants.MarkerFile.Server}\" />");
        sb.AppendLine(2, "</ItemGroup>");
        sb.AppendLine();
        sb.Append("</Project>");

        return sb.ToString();
    }

    private static string GenerateDomainProjectFileContent(
        List<string> specFilePaths,
        string contractsProjectName,
        Version sourceGeneratorVersion)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        sb.AppendLine();
        sb.AppendLine(2, "<PropertyGroup>");
        sb.AppendLine(4, $"<TargetFramework>{TargetFrameworkResult.RequiredTargetFramework}</TargetFramework>");
        sb.AppendLine(2, "</PropertyGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");
        sb.AppendLine(4, "<FrameworkReference Include=\"Microsoft.AspNetCore.App\" />");
        sb.AppendLine(2, "</ItemGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");
        sb.AppendLine(4, $"<ProjectReference Include=\"..\\{contractsProjectName}\\{contractsProjectName}.csproj\" />");
        sb.AppendLine(2, "</ItemGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");
        sb.AppendLine(4, $"<PackageReference Include=\"Atc.Rest.Api.SourceGenerator\" Version=\"{sourceGeneratorVersion}\" OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\" />");
        sb.AppendLine(2, "</ItemGroup>");
        sb.AppendLine();
        sb.AppendLine(2, "<ItemGroup>");

        foreach (var specPath in specFilePaths)
        {
            sb.AppendLine(4, $"<AdditionalFiles Include=\"{specPath}\" />");
        }

        sb.AppendLine(4, $"<AdditionalFiles Include=\"{Constants.MarkerFile.ServerHandlers}\" />");
        sb.AppendLine(2, "</ItemGroup>");
        sb.AppendLine();
        sb.Append("</Project>");

        return sb.ToString();
    }

    private static bool UpdateProjectFileIfNeeded(
        string csprojPath,
        List<string> specFilePaths,
        string markerFileName)
    {
        var content = File.ReadAllText(csprojPath);
        var originalContent = content;

        // Check which spec files are missing
        var missingSpecFiles = specFilePaths
            .Where(specPath => !content.Contains($"Include=\"{specPath}\"", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var hasMarkerInclude = content.Contains(markerFileName, StringComparison.OrdinalIgnoreCase);

        if (missingSpecFiles.Count == 0 && hasMarkerInclude)
        {
            return false;
        }

        if (content.Contains("<AdditionalFiles", StringComparison.OrdinalIgnoreCase))
        {
            // Add missing spec files, then the marker - each a line of its own before the list's closing tag
            foreach (var specPath in missingSpecFiles)
            {
                content = InsertAdditionalFile(content, specPath);
            }

            if (!hasMarkerInclude)
            {
                content = InsertAdditionalFile(content, markerFileName);
            }
        }
        else
        {
            var insertPosition = content.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
            if (insertPosition >= 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("  <ItemGroup>");

                foreach (var specPath in specFilePaths)
                {
                    sb.Append("    <AdditionalFiles Include=\"");
                    sb.Append(specPath);
                    sb.AppendLine("\" />");
                }

                sb.Append("    <AdditionalFiles Include=\"");
                sb.Append(markerFileName);
                sb.AppendLine("\" />");
                sb.AppendLine("  </ItemGroup>");
                sb.AppendLine();

                content = content.Insert(insertPosition, sb.ToString());
            }
        }

        if (content != originalContent)
        {
            TrackedFile.WriteAllText(csprojPath, content);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Inserts <c>&lt;AdditionalFiles Include="…" /&gt;</c> as a line of its own before the line of the last
    /// <c>&lt;/ItemGroup&gt;</c> that follows an <c>AdditionalFiles</c> item, in the file's own line endings; the content
    /// unchanged when there is no such list.
    /// </summary>
    private static string InsertAdditionalFile(
        string content,
        string include)
    {
        var closingTag = content.LastIndexOf("</ItemGroup>", StringComparison.OrdinalIgnoreCase);
        if (closingTag < 0 ||
            content.LastIndexOf("<AdditionalFiles", closingTag, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return content;
        }

        var newLine = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lineStart = content.LastIndexOf('\n', closingTag) + 1;
        return content.Insert(lineStart, $"    <AdditionalFiles Include=\"{include}\" />{newLine}");
    }
}