namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to execute migration from old CLI-generated API to source generators.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class MigrateExecuteCommand : Command<MigrateExecuteCommandSettings>
{
    private readonly INugetPackageVersionService nugetService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrateExecuteCommand"/> class.
    /// </summary>
    /// <param name="nugetService">Resolves the source generator package version written into the migrated project files.</param>
    public MigrateExecuteCommand(INugetPackageVersionService nugetService)
    {
        this.nugetService = nugetService;
    }

    /// <summary>
    /// Re-runs the migration validation and, when the solution is ready, rewrites it for the source generators.
    /// </summary>
    /// <param name="context">The Spectre.Console command context.</param>
    /// <param name="settings">The parsed command-line settings.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>0 when the migration (or dry run) completed; otherwise 1.</returns>
    public override int Execute(
        CommandContext context,
        MigrateExecuteCommandSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        WriteHeader();

        var solutionPath = Path.GetFullPath(settings.SolutionPath);
        var specPath = Path.GetFullPath(settings.SpecificationPath);

        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Solution:[/]      {solutionPath}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Specification:[/] {specPath}");
        foreach (var note in settings.PathNotes)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Note:[/]          {note}");
        }

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]Mode:[/]          Dry Run (no changes will be made)");
        }

        AnsiConsole.WriteLine();

        try
        {
            // Step 1: Run validation
            var validationSettings = new MigrateValidateCommandSettings
            {
                SolutionPath = settings.SolutionPath,
                SpecificationPath = settings.SpecificationPath,
            };

            var report = MigrationValidator.Validate(validationSettings);

            if (!report.CanMigrate)
            {
                AnsiConsole.MarkupLine("[red]✗ Project cannot be migrated. Please fix the blocking issues first.[/]");
                foreach (var issue in report.BlockingIssues)
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red]•[/] {issue}");
                }

                return 1;
            }

            // What validate would warn about, before anything changes
            foreach (var warning in report.Warnings)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] {warning}");
            }

            // Step 2: Git status check
            var rootDirectory = validationSettings.GetSolutionDirectory();
            if (!CheckGitStatus(rootDirectory, settings.Force, settings.DryRun))
            {
                AnsiConsole.MarkupLine("[yellow]Migration cancelled by user.[/]");
                return 1;
            }

            // Step 3: Confirm upgrade if needed
            if (report.RequiresUpgrade &&
                !settings.Force &&
                !ConfirmUpgrade(report))
            {
                AnsiConsole.MarkupLine("[yellow]Migration cancelled. .NET 10 / C# 14 upgrade is required.[/]");
                return 1;
            }

            // Execute migration
            return ExecuteMigration(settings, report, rootDirectory, specPath, cancellationToken);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗ Error:[/] {ex.Message}");
            return 1;
        }
    }

    internal static void UpdateGlobalUsings(
        MigrationValidationReport report,
        string projectName,
        bool dryRun,
        MigrationSummary summary)
    {
        var contractsNamespace = $"{projectName}.Api.Contracts";

        // Process Domain project
        if (!string.IsNullOrEmpty(report.ProjectStructure.DomainProject))
        {
            var domainDir = Path.GetDirectoryName(report.ProjectStructure.DomainProject)!;
            ProcessGlobalUsingsForProject(report, domainDir, contractsNamespace, addGeneratedUsings: true, dryRun, summary);
        }

        // Process Domain.Tests project: only what its code used (its references were rewritten); every segment's
        // namespaces would make a type ambiguous with another project's type of the same name
        if (!string.IsNullOrEmpty(report.ProjectStructure.DomainTestProject))
        {
            var testDir = Path.GetDirectoryName(report.ProjectStructure.DomainTestProject)!;
            ProcessGlobalUsingsForProject(report, testDir, contractsNamespace, addGeneratedUsings: false, dryRun, summary);
        }
    }

    /// <summary>
    /// Rewrites the references to the old generator's contracts (<c>{Project}.Api.Generated.Contracts[.{Segment}]</c>) to
    /// the source generator's namespaces in every C# file of the solution - every project that used the contracts, not
    /// only the domain - outside build output, laid out from the specification.
    /// </summary>
    /// <summary>
    /// Describes the packages the source generator makes obsolete, each with its direct dependencies (from its .nuspec
    /// in <paramref name="packagesFolder"/>): removing a package drops what it brought in transitively, which the host
    /// may still use.
    /// </summary>
    internal static List<string> DescribeObsoletePackages(
        PackageReferenceResult references,
        string packagesFolder)
        => references.PackagesToRemove
            .Select(package =>
            {
                var version = references.AtcPackages.GetValueOrDefault(package, string.Empty);
                var dependencies = version.Length == 0 ? null : NuGetPackageDependencies.Read(package, version, packagesFolder);
                return dependencies switch
                {
                    null => $"{package} {version} - its dependencies are unknown (the package is not in the NuGet cache)",
                    { Count: 0 } => $"{package} {version} - it brings no other package",
                    _ => $"{package} {version} - add these as direct references if the build needs them: {string.Join(", ", dependencies)}",
                };
            })
            .ToList();

    /// <summary>
    /// Renames the old generated projects (Api.Generated to Api.Contracts, ApiClient.Generated to the client name).
    /// </summary>
    /// <returns>What stood in the way of a rename that left no project under the new name, per project; empty when every project is there under its new name.</returns>
    internal static List<string> RenameProjects(
        MigrationValidationReport report,
        string projectName,
        string? clientSuffix,
        bool dryRun,
        MigrationSummary summary)
    {
        var errors = new List<string>();

        // Rename Api.Generated → Api.Contracts
        if (!string.IsNullOrEmpty(report.ProjectStructure.ApiGeneratedProject))
        {
            var serverDir = Path.GetDirectoryName(report.ProjectStructure.ApiGeneratedProject)!;
            var result = ProjectRenamer.RenameServerProject(serverDir, projectName, dryRun);
            if (result.Success)
            {
                summary.RenamedProjects.Add($"{result.OldName} → {result.NewName}");

                // Report old directory deletion when target already existed
                if (result.OldDirectoryDeleted)
                {
                    summary.DeletedFolders.Add(result.OldPath);
                }
            }
            else if (!string.IsNullOrEmpty(result.Error))
            {
                // Without the new folder nothing the next steps write would exist: stop. With it (the old folder is
                // left beside it, holding files) the new names are real: say so and go on.
                if (string.IsNullOrEmpty(result.NewPath) || !Directory.Exists(result.NewPath))
                {
                    errors.Add(result.Error);
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Warning:[/] {result.Error}");
                }
            }
        }

        // Rename ApiClient.Generated → client suffix (default: ApiClient)
        if (!string.IsNullOrEmpty(report.ProjectStructure.ApiClientGeneratedProject))
        {
            var clientDir = Path.GetDirectoryName(report.ProjectStructure.ApiClientGeneratedProject)!;
            var result = ProjectRenamer.RenameClientProject(clientDir, projectName, clientSuffix, dryRun);
            if (result.Success)
            {
                summary.RenamedProjects.Add($"{result.OldName} → {result.NewName}");

                // Report old directory deletion when target already existed
                if (result.OldDirectoryDeleted)
                {
                    summary.DeletedFolders.Add(result.OldPath);
                }
            }
            else if (!string.IsNullOrEmpty(result.Error))
            {
                // Without the new folder nothing the next steps write would exist: stop. With it (the old folder is
                // left beside it, holding files) the new names are real: say so and go on.
                if (string.IsNullOrEmpty(result.NewPath) || !Directory.Exists(result.NewPath))
                {
                    errors.Add(result.Error);
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Warning:[/] {result.Error}");
                }
            }
        }

        // A dry run only says what it would do
        if (dryRun)
        {
            foreach (var error in errors)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Warning:[/] {error}");
            }
        }

        return errors;
    }

    internal static void UpdateContractsReferences(
        MigrationValidationReport report,
        string projectName,
        string solutionRoot,
        bool dryRun,
        MigrationSummary summary)
    {
        if (string.IsNullOrEmpty(report.SpecificationPath) ||
            !File.Exists(report.SpecificationPath) ||
            !Directory.Exists(solutionRoot))
        {
            return;
        }

        var (_, merged) = SpecificationService.ReadAndMergeResolved(report.SpecificationPath);
        if (merged?.Document is null)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] the specification could not be read; the contracts namespaces were not rewritten.");
            return;
        }

        var map = ContractsNamespaceMap.Build(merged.Document, projectName);
        var sources = new List<(string Path, string Content)>();
        foreach (var filePath in Directory.EnumerateFiles(solutionRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(solutionRoot, filePath))
            {
                continue;
            }

            var content = File.ReadAllText(filePath);
            var (updated, replacements) = content.Contains("Api.Generated.Contracts", StringComparison.Ordinal)
                ? ContractsNamespaceRewriter.Rewrite(content, map)
                : (content, Array.Empty<string>());
            sources.Add((Path.GetRelativePath(solutionRoot, filePath), updated));
            if (replacements.Count == 0)
            {
                continue;
            }

            if (!dryRun)
            {
                File.WriteAllText(filePath, updated);
            }

            var fileUpdate = new DomainFileUpdate { FilePath = filePath };
            fileUpdate.Replacements.AddRange(replacements);
            summary.DomainCodeUpdates.Add(fileUpdate);
            summary.DomainCodeTotalReplacements += replacements.Count;
            if (!summary.ModifiedFiles.Contains(filePath, StringComparer.Ordinal))
            {
                summary.ModifiedFiles.Add(filePath);
            }
        }

        // The old call shapes the generated code no longer fits - reported for the user to review
        summary.CallShapeFindings.AddRange(CallShapeDetector.Detect(merged.Document, sources));
    }

    internal static void UpdateHostProject(
        MigrationValidationReport report,
        string projectName,
        bool dryRun,
        MigrationSummary summary)
    {
        if (string.IsNullOrEmpty(report.ProjectStructure.HostApiProject))
        {
            return;
        }

        var hostDir = Path.GetDirectoryName(report.ProjectStructure.HostApiProject)!;

        // Required namespaces for MapEndpoints() and AddApiHandlersFromDomain()
        var requiredNamespaces = new List<string>
        {
            $"{projectName}.Api.Contracts",
            $"{projectName}.Api.Contracts.Generated.Endpoints",
        };

        // Update GlobalUsings.cs - remove old patterns, add required, sort properly
        var globalUsingsResult = GlobalUsingsModifier.CleanupAndSortGlobalUsings(
            hostDir,
            projectName,
            requiredNamespaces,
            dryRun: dryRun);

        if (globalUsingsResult.WasModified)
        {
            if (!summary.ModifiedFiles.Contains(globalUsingsResult.FilePath, StringComparer.Ordinal))
            {
                summary.ModifiedFiles.Add(globalUsingsResult.FilePath);
            }

            summary.HostProjectUpdates.AddRange(globalUsingsResult.RemovedUsings.Select(ns => $"- {ns}"));
            summary.HostProjectUpdates.AddRange(globalUsingsResult.AddedUsings.Select(ns => $"+ {ns}"));
            summary.HostProjectUpdates.AddRange(globalUsingsResult.UpdatedUsings);
        }

        // Update Program.cs - remove old patterns, add new ones
        var programResult = ProgramCsModifier.UpdateToSourceGenerator(hostDir, projectName, dryRun);
        if (programResult.WasModified)
        {
            if (!summary.ModifiedFiles.Contains(programResult.FilePath, StringComparer.Ordinal))
            {
                summary.ModifiedFiles.Add(programResult.FilePath);
            }

            summary.HostProjectUpdates.AddRange(programResult.RemovedStatements.Select(s => $"- {s}"));
            summary.HostProjectUpdates.AddRange(programResult.ReplacedStatements);
            summary.HostProjectUpdates.AddRange(programResult.AddedStatements.Select(s => $"+ {s}"));
        }

        if (!string.IsNullOrEmpty(globalUsingsResult.Error))
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Warning:[/] {globalUsingsResult.Error}");
        }

        if (!string.IsNullOrEmpty(programResult.Error))
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Warning:[/] {programResult.Error}");
        }
    }

    private int ExecuteMigration(
        MigrateExecuteCommandSettings settings,
        MigrationValidationReport report,
        string rootDirectory,
        string specPath,
        CancellationToken cancellationToken)
    {
        var projectName = report.DetectedProjectName ?? "Unknown";
        var dryRun = settings.DryRun;

        return AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .Start(dryRun ? "Analyzing migration changes..." : "Executing migration...", ctx =>
            {
                var summary = new MigrationSummary();

                // Step 4: Copy specification (stays in place, just track it)
                ctx.Status("Analyzing specification location...");
                var specRelativePath = GetRelativeSpecPath(specPath, report.ProjectStructure.ApiGeneratedProject);
                summary.SpecificationPath = specRelativePath;

                // Step 5: Generate marker files
                ctx.Status(dryRun ? "Analyzing marker files..." : "Generating marker files...");
                GenerateMarkerFiles(report, projectName, settings.ClientProjectSuffix, settings.HttpClientName, dryRun, summary);

                // Step 6: Resolve package versions from NuGet (with fallback)
                ctx.Status("Resolving package versions...");
                var sourceGeneratorVersion = TaskHelper.RunSync(() => nugetService.GetLatestVersionOrFallbackAsync(
                    PackageVersionDefaults.SourceGeneratorPackageId,
                    PackageVersionDefaults.SourceGeneratorFallback,
                    cancellationToken));
                var restClientVersion = TaskHelper.RunSync(() => nugetService.GetLatestVersionOrFallbackAsync(
                    PackageVersionDefaults.RestClientPackageId,
                    PackageVersionDefaults.RestClientMinFallback,
                    cancellationToken));

                // Step 7: Modify project files
                ctx.Status(dryRun ? "Analyzing project modifications..." : "Modifying project files...");
                ModifyProjectFiles(
                    report,
                    specRelativePath,
                    sourceGeneratorVersion,
                    restClientVersion,
                    dryRun,
                    summary);

                // Step 8: Handle ATC coding rules
                ctx.Status(dryRun ? "Analyzing ATC coding rules..." : "Handling ATC coding rules...");
                HandleAtcCodingRules(rootDirectory, settings.Force, dryRun, summary);

                // Step 8b: Upgrade Directory.Build.props if needed
                if (report.RequiresUpgrade)
                {
                    ctx.Status(dryRun ? "Analyzing Directory.Build.props..." : "Upgrading Directory.Build.props...");
                    UpgradeDirectoryBuildProps(rootDirectory, dryRun, summary);
                }

                // Step 9: Clean generated code
                ctx.Status(dryRun ? "Analyzing generated code cleanup..." : "Cleaning generated code...");
                CleanGeneratedCode(report, dryRun, summary);

                // Step 10: Rename projects
                ctx.Status(dryRun ? "Analyzing project renames..." : "Renaming projects...");
                var renameErrors = RenameProjects(report, projectName, settings.ClientProjectSuffix, dryRun, summary);
                if (renameErrors.Count > 0 && !dryRun)
                {
                    // The steps after this write the new project names into the solution and every reference: with a
                    // project still under its old name they would point at folders that are not there
                    foreach (var renameError in renameErrors)
                    {
                        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]Error:[/] {renameError}");
                    }

                    AnsiConsole.MarkupLine("[red]The migration stopped before the solution and the references were updated.[/] Fix what is in the way and run the command again to complete it.");
                    return 1;
                }

                // Step 11: Update solution and references
                ctx.Status(dryRun ? "Analyzing solution updates..." : "Updating solution and references...");
                UpdateSolutionAndReferences(report, projectName, settings.ClientProjectSuffix, dryRun, summary);

                // Step 12: Rewrite the old contracts namespaces in every project of the solution
                ctx.Status(dryRun ? "Analyzing contracts namespaces..." : "Updating contracts namespaces...");
                UpdateContractsReferences(report, projectName, rootDirectory, dryRun, summary);

                // Step 13: Update GlobalUsings.cs in the Domain project (and its tests)
                ctx.Status(dryRun ? "Analyzing GlobalUsings.cs..." : "Updating GlobalUsings.cs...");
                UpdateGlobalUsings(report, projectName, dryRun, summary);

                // Step 14: Migrate parameter property names in handler code
                ctx.Status(dryRun ? "Analyzing parameter names..." : "Migrating parameter names...");
                MigrateParameterNames(report, dryRun, summary);

                // Step 15: Update Host project (Program.cs and GlobalUsings)
                ctx.Status(dryRun ? "Analyzing Host project..." : "Updating Host project...");
                UpdateHostProject(report, projectName, dryRun, summary);

                // The packages the source generator makes obsolete, with what removing them would drop
                summary.ObsoletePackages.AddRange(DescribeObsoletePackages(report.PackageReferences, NuGetPackageDependencies.DefaultPackagesFolder));

                // Display summary
                DisplayMigrationSummary(summary, dryRun);

                if (!dryRun)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[green]✓ Migration completed successfully![/]");
                    AnsiConsole.WriteLine();

                    var nextStepsPanel = new Panel(
                        new Rows(
                            new Markup("1. Review [blue]Program.cs[/] and [blue]ServiceCollectionExtensions.cs[/] in the Host project"),
                            new Markup("   to remove obsolete code (old endpoint registrations, unused services)"),
                            new Markup("2. Run [blue]'dotnet build'[/] to verify the migration"),
                            new Markup("3. Review the generated code in the Contracts project"),
                            new Markup("4. Commit the changes to git"),
                            new Markup(string.Empty),
                            new Markup("[dim]For more information, see the wiki:[/]"),
                            new Markup("[link]https://github.com/atc-net/atc-rest-api-source-generator/wiki[/]")))
                        .Header("[yellow]Next Steps[/]")
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Grey);

                    AnsiConsole.Write(nextStepsPanel);
                }

                return 0;
            });
    }

    private static void GenerateMarkerFiles(
        MigrationValidationReport report,
        string projectName,
        string? clientSuffix,
        string? httpClientName,
        bool dryRun,
        MigrationSummary summary)
    {
        // Server marker
        if (!string.IsNullOrEmpty(report.ProjectStructure.ApiGeneratedProject))
        {
            var serverDir = Path.GetDirectoryName(report.ProjectStructure.ApiGeneratedProject)!;
            var markerPath = MarkerFileGenerator.GenerateServerMarker(
                serverDir,
                report.GeneratorOptions,
                projectName,
                dryRun);
            summary.CreatedFiles.Add(markerPath);
        }

        // Client marker
        if (!string.IsNullOrEmpty(report.ProjectStructure.ApiClientGeneratedProject))
        {
            var clientDir = Path.GetDirectoryName(report.ProjectStructure.ApiClientGeneratedProject)!;
            var markerPath = MarkerFileGenerator.GenerateClientMarker(
                clientDir,
                report.GeneratorOptions,
                projectName,
                clientSuffix,
                httpClientName,
                dryRun);
            summary.CreatedFiles.Add(markerPath);
        }

        // Handler marker
        if (!string.IsNullOrEmpty(report.ProjectStructure.DomainProject))
        {
            var domainDir = Path.GetDirectoryName(report.ProjectStructure.DomainProject)!;
            var markerPath = MarkerFileGenerator.GenerateHandlerMarker(
                domainDir,
                report.GeneratorOptions,
                projectName,
                dryRun);
            summary.CreatedFiles.Add(markerPath);
        }
    }

    private static void ModifyProjectFiles(
        MigrationValidationReport report,
        string specRelativePath,
        Version sourceGeneratorVersion,
        Version restClientMinVersion,
        bool dryRun,
        MigrationSummary summary)
    {
        // Modify Api.Generated project
        if (!string.IsNullOrEmpty(report.ProjectStructure.ApiGeneratedProject))
        {
            var result = ProjectFileModifier.ModifyServerProject(
                report.ProjectStructure.ApiGeneratedProject,
                specRelativePath,
                sourceGeneratorVersion,
                dryRun);
            if (result.WasModified)
            {
                summary.ModifiedFiles.Add(result.ProjectPath);
            }
        }

        // Modify ApiClient.Generated project
        if (!string.IsNullOrEmpty(report.ProjectStructure.ApiClientGeneratedProject))
        {
            var clientSpecPath = GetRelativeSpecPathForClient(
                report.ProjectStructure.ApiClientGeneratedProject,
                report.ProjectStructure.ApiGeneratedProject,
                specRelativePath);

            var result = ProjectFileModifier.ModifyClientProject(
                report.ProjectStructure.ApiClientGeneratedProject,
                clientSpecPath,
                sourceGeneratorVersion,
                restClientMinVersion,
                dryRun);
            if (result.WasModified)
            {
                summary.ModifiedFiles.Add(result.ProjectPath);
            }
        }

        // Modify Domain project
        if (!string.IsNullOrEmpty(report.ProjectStructure.DomainProject))
        {
            var domainSpecPath = GetRelativeSpecPathForDomain(
                report.ProjectStructure.DomainProject,
                report.ProjectStructure.ApiGeneratedProject,
                specRelativePath);

            var result = ProjectFileModifier.ModifyDomainProject(
                report.ProjectStructure.DomainProject,
                domainSpecPath,
                sourceGeneratorVersion,
                dryRun);
            if (result.WasModified)
            {
                summary.ModifiedFiles.Add(result.ProjectPath);
            }
        }
    }

    private static void HandleAtcCodingRules(
        string rootDirectory,
        bool force,
        bool dryRun,
        MigrationSummary summary)
    {
        var result = AtcCodingRulesHandler.Check(rootDirectory);

        if (result.ConfigExists && result.NeedsUpdate)
        {
            var updated = AtcCodingRulesHandler.UpdateProjectTarget(result.ConfigPath!, "DotNet10", dryRun);
            if (updated)
            {
                summary.ModifiedFiles.Add(result.ConfigPath!);
                summary.AtcCodingRulesUpdated = true;
            }
        }
        else if (!result.ConfigExists && !dryRun && !force)
        {
            if (AtcCodingRulesHandler.PromptForSetup())
            {
                var configPath = AtcCodingRulesHandler.CreateConfig(rootDirectory, "DotNet10", dryRun);
                summary.CreatedFiles.Add(configPath);
                summary.AtcCodingRulesCreated = true;
            }
        }
        else if (!result.ConfigExists && dryRun)
        {
            summary.AtcCodingRulesWillPrompt = true;
        }
    }

    private static void CleanGeneratedCode(
        MigrationValidationReport report,
        bool dryRun,
        MigrationSummary summary)
    {
        // Clean Api.Generated
        if (!string.IsNullOrEmpty(report.ProjectStructure.ApiGeneratedProject))
        {
            var serverDir = Path.GetDirectoryName(report.ProjectStructure.ApiGeneratedProject)!;
            var result = GeneratedCodeCleaner.CleanServerProject(serverDir, dryRun);
            foreach (var folder in result.DeletedFolders)
            {
                summary.DeletedFolders.Add($"{folder.Path} ({folder.ItemCount} files)");
            }

            summary.DeletedFiles.AddRange(result.DeletedFiles);
            summary.PreservedFiles.AddRange(result.PreservedFiles);
        }

        // Clean ApiClient.Generated
        if (!string.IsNullOrEmpty(report.ProjectStructure.ApiClientGeneratedProject))
        {
            var clientDir = Path.GetDirectoryName(report.ProjectStructure.ApiClientGeneratedProject)!;
            var result = GeneratedCodeCleaner.CleanClientProject(clientDir, dryRun);
            foreach (var folder in result.DeletedFolders)
            {
                summary.DeletedFolders.Add($"{folder.Path} ({folder.ItemCount} files)");
            }

            summary.DeletedFiles.AddRange(result.DeletedFiles);
            summary.PreservedFiles.AddRange(result.PreservedFiles);
        }
    }

    private static void UpdateSolutionAndReferences(
        MigrationValidationReport report,
        string projectName,
        string? clientSuffix,
        bool dryRun,
        MigrationSummary summary)
    {
        // Update solution file
        if (!string.IsNullOrEmpty(report.ProjectStructure.SolutionFile))
        {
            var result = SolutionModifier.UpdateAllReferences(
                report.ProjectStructure.SolutionFile,
                projectName,
                clientSuffix,
                dryRun);
            if (result.WasModified)
            {
                summary.ModifiedFiles.Add(result.SolutionPath);
                summary.UpdatedReferences.AddRange(result.UpdatedReferences);
            }
        }

        // Update all project references
        var projectResults = SolutionModifier.UpdateAllProjectReferences(
            Path.GetDirectoryName(report.ProjectStructure.SolutionFile) ?? string.Empty,
            report.ProjectStructure.AllProjects,
            projectName,
            clientSuffix,
            dryRun);

        foreach (var result in projectResults)
        {
            if (result.WasModified)
            {
                summary.ModifiedFiles.Add(result.ProjectPath);
                summary.UpdatedReferences.AddRange(result.UpdatedReferences);
            }
        }
    }

    private static void ProcessGlobalUsingsForProject(
        MigrationValidationReport report,
        string projectDir,
        string contractsNamespace,
        bool addGeneratedUsings,
        bool dryRun,
        MigrationSummary summary)
    {
        var projectName = report.DetectedProjectName ?? string.Empty;

        // Step 1: Update existing namespace references (Api.Generated -> Api.Contracts.Generated)
        var updateResult = GlobalUsingsModifier.UpdateNamespaces(projectDir, projectName, dryRun);
        if (updateResult.WasModified)
        {
            summary.ModifiedFiles.Add(updateResult.FilePath);
            summary.UpdatedGlobalUsings.AddRange(updateResult.UpdatedUsings);
        }

        // Step 2: Remove old CLI-generated patterns (*.Generated.Contracts.*, etc.)
        var removeResult = GlobalUsingsModifier.RemoveOldGeneratedUsings(projectDir, contractsNamespace, dryRun);
        if (removeResult.WasModified)
        {
            if (!summary.ModifiedFiles.Contains(removeResult.FilePath, StringComparer.Ordinal))
            {
                summary.ModifiedFiles.Add(removeResult.FilePath);
            }

            summary.UpdatedGlobalUsings.AddRange(removeResult.RemovedUsings.Select(ns => $"- {ns}"));
        }

        // Step 3: Add new generated namespace usings based on the OpenAPI spec (the domain's handlers need them all)
        if (addGeneratedUsings && !string.IsNullOrEmpty(report.SpecificationPath))
        {
            var addResult = GlobalUsingsModifier.AddGeneratedNamespaceUsings(
                projectDir,
                report.SpecificationPath,
                contractsNamespace,
                dryRun);

            if (addResult.WasModified)
            {
                if (!summary.ModifiedFiles.Contains(addResult.FilePath, StringComparer.Ordinal))
                {
                    summary.ModifiedFiles.Add(addResult.FilePath);
                }

                summary.UpdatedGlobalUsings.AddRange(addResult.AddedUsings.Select(ns => $"+ {ns}"));
            }

            if (!string.IsNullOrEmpty(addResult.Error))
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Warning:[/] {addResult.Error}");
            }
        }

        // Step 4: Sort global usings (System first, then by namespace group)
        GlobalUsingsModifier.SortGlobalUsings(projectDir, dryRun: dryRun);
    }

    private static readonly char[] PathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    private static bool IsBuildOutput(
        string solutionRoot,
        string filePath)
    {
        var relative = Path.GetRelativePath(solutionRoot, filePath);
        return relative
            .Split(PathSeparators)
            .Any(part => part is "bin" or "obj" or "node_modules" or ".git");
    }

    private static void MigrateParameterNames(
        MigrationValidationReport report,
        bool dryRun,
        MigrationSummary summary)
    {
        if (string.IsNullOrEmpty(report.SpecificationPath))
        {
            return;
        }

        // Collect directories to migrate: Domain and Domain.Tests
        var directoriesToMigrate = new List<string>();

        if (!string.IsNullOrEmpty(report.ProjectStructure.DomainProject))
        {
            directoriesToMigrate.Add(Path.GetDirectoryName(report.ProjectStructure.DomainProject)!);
        }

        if (!string.IsNullOrEmpty(report.ProjectStructure.DomainTestProject))
        {
            directoriesToMigrate.Add(Path.GetDirectoryName(report.ProjectStructure.DomainTestProject)!);
        }

        if (directoriesToMigrate.Count == 0)
        {
            return;
        }

        foreach (var directory in directoriesToMigrate)
        {
            var result = ParameterNameMigrator.MigrateParameterNames(directory, report.SpecificationPath, dryRun);

            if (result.ReplacedParameters.Count > 0)
            {
                summary.ParameterMigrations.AddRange(result.ReplacedParameters);

                foreach (var file in result.ModifiedFiles)
                {
                    if (!summary.ModifiedFiles.Contains(file, StringComparer.Ordinal))
                    {
                        summary.ModifiedFiles.Add(file);
                    }
                }
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]Warning:[/] {result.Error}");
            }
        }
    }

    private static void UpgradeDirectoryBuildProps(
        string rootDirectory,
        bool dryRun,
        MigrationSummary summary)
    {
        var targetFramework = TargetFrameworkResult.RequiredTargetFramework;
        var langVersion = TargetFrameworkResult.RequiredLangVersion;

        var upgraded = DirectoryBuildPropsModifier.UpgradeTargetFramework(
            rootDirectory,
            targetFramework,
            langVersion,
            dryRun);

        if (upgraded)
        {
            summary.DirectoryBuildPropsUpgraded = true;
            summary.UpgradedTargetFramework = targetFramework;
            summary.UpgradedLangVersion = langVersion;
            summary.ModifiedFiles.Add(Path.Combine(rootDirectory, "Directory.Build.props"));
        }

        // Also upgrade any .csproj files that have explicit TargetFramework elements
        var upgradedCsprojFiles = DirectoryBuildPropsModifier.UpgradeCsprojTargetFrameworks(
            rootDirectory,
            targetFramework,
            dryRun);

        foreach (var csprojFile in upgradedCsprojFiles)
        {
            summary.ModifiedFiles.Add(csprojFile);

            // Mark as upgraded even if Directory.Build.props wasn't modified
            if (!summary.DirectoryBuildPropsUpgraded)
            {
                summary.DirectoryBuildPropsUpgraded = true;
                summary.UpgradedTargetFramework = targetFramework;
                summary.UpgradedLangVersion = langVersion;
            }
        }
    }

    private static void DisplayMigrationSummary(
        MigrationSummary summary,
        bool dryRun)
    {
        AnsiConsole.WriteLine();

        if (dryRun)
        {
            AnsiConsole.MarkupLine("[blue]Migration Summary (Dry Run)[/]");
            AnsiConsole.WriteLine();
        }

        if (summary.CreatedFiles.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Files to Create[/]");
            foreach (var file in summary.CreatedFiles)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [green]+[/] {file}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.DeletedFolders.Count > 0 || summary.DeletedFiles.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Files/Folders to Delete[/]");
            foreach (var folder in summary.DeletedFolders)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red]-[/] {folder}");
            }

            foreach (var file in summary.DeletedFiles)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red]-[/] {file}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.CallShapeFindings.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Code written for the old generator's shapes ({summary.CallShapeFindings.Count}, best-effort - review each)[/]");
            foreach (var finding in summary.CallShapeFindings.Take(100))
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [yellow]![/] {finding}");
            }

            if (summary.CallShapeFindings.Count > 100)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]... and {summary.CallShapeFindings.Count - 100} more[/]");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.ObsoletePackages.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Packages the source generator makes obsolete (remove them when nothing else uses them)[/]");
            foreach (var package in summary.ObsoletePackages)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [yellow]![/] {package}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.PreservedFiles.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Hand-written files kept in the generated folders (move them where they belong)[/]");
            foreach (var file in summary.PreservedFiles)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [yellow]![/] {file}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.ModifiedFiles.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Files to Modify[/]");
            foreach (var file in summary.ModifiedFiles.Distinct(StringComparer.Ordinal))
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [yellow]~[/] {file}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.RenamedProjects.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Projects to Rename[/]");
            foreach (var rename in summary.RenamedProjects)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [cyan]↳[/] {rename}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.UpdatedReferences.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Project References to Update[/]");
            foreach (var reference in summary.UpdatedReferences)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [cyan]↳[/] {reference}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.UpdatedGlobalUsings.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]GlobalUsings.cs Namespace Updates[/]");
            foreach (var update in summary.UpdatedGlobalUsings)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [cyan]↳[/] {update}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.DirectoryBuildPropsUpgraded)
        {
            AnsiConsole.MarkupLine("[blue]Directory.Build.props Upgrade[/]");
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [cyan]↳[/] TargetFramework → {summary.UpgradedTargetFramework}");
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [cyan]↳[/] LangVersion → {summary.UpgradedLangVersion}");
            AnsiConsole.WriteLine();
        }

        if (summary.DomainCodeUpdates.Count > 0)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[blue]Domain Code Namespace Updates ({summary.DomainCodeTotalReplacements} replacements)[/]");
            foreach (var fileUpdate in summary.DomainCodeUpdates)
            {
                var relativePath = Path.GetFileName(fileUpdate.FilePath);
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [yellow]~[/] {relativePath} ({fileUpdate.Replacements.Count} replacements)");
                foreach (var replacement in fileUpdate.Replacements)
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"    [cyan]↳[/] {replacement}");
                }
            }

            AnsiConsole.WriteLine();
        }

        if (summary.HostProjectUpdates.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Host Project Updates[/]");
            foreach (var update in summary.HostProjectUpdates)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [cyan]↳[/] {update}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.ParameterMigrations.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Parameter Name Migrations[/]");
            foreach (var migration in summary.ParameterMigrations)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [cyan]↳[/] {migration}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.AtcCodingRulesWillPrompt && dryRun)
        {
            AnsiConsole.MarkupLine("[yellow]![/] Will prompt to set up atc-coding-rules-updater");
            AnsiConsole.WriteLine();
        }

        var deleteCount = summary.DeletedFolders.Count + summary.DeletedFiles.Count;
        var modifyCount = summary.ModifiedFiles.Distinct(StringComparer.Ordinal).Count();
        AnsiConsole.MarkupLineInterpolated(
            CultureInfo.InvariantCulture,
            $"[dim]Summary: {summary.CreatedFiles.Count} create, {deleteCount} delete, {modifyCount} modify, {summary.RenamedProjects.Count} rename[/]");
    }

    private static bool CheckGitStatus(
        string rootDirectory,
        bool force,
        bool dryRun)
    {
        var gitStatus = GitStatusChecker.Check(rootDirectory);

        if (!gitStatus.IsGitRepository)
        {
            AnsiConsole.MarkupLine("[yellow]![/] Not a git repository. Cannot check for uncommitted changes.");
            if (!force)
            {
                return AnsiConsole.Confirm("Do you want to proceed anyway?", defaultValue: false);
            }

            return true;
        }

        if (gitStatus.GitCommandFailed)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] Git command failed: {gitStatus.ErrorMessage}");
            return true;
        }

        if (gitStatus.HasUncommittedChanges)
        {
            if (dryRun)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]⚠ Git status: {gitStatus.UncommittedFiles.Count} uncommitted changes detected[/]");
                AnsiConsole.MarkupLine("  [dim](Use --force to skip this check)[/]");
                return true;
            }

            if (force)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]⚠ Git status: {gitStatus.UncommittedFiles.Count} uncommitted changes (--force specified)[/]");
                return true;
            }

            return GitStatusChecker.DisplayWarningAndConfirm(gitStatus);
        }

        AnsiConsole.MarkupLine("[green]✓[/] Git status: No uncommitted changes");
        return true;
    }

    private static bool ConfirmUpgrade(MigrationValidationReport report)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]![/] Target framework and language version upgrade required");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Current configuration:[/]");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  • Target framework: {report.TargetFramework.CurrentTargetFramework ?? "unknown"} → {TargetFrameworkResult.RequiredTargetFramework}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  • Language version: C# {report.TargetFramework.CurrentLangVersion ?? "unknown"} → C# {TargetFrameworkResult.RequiredLangVersion}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"The source generator requires .NET 10 with C# {TargetFrameworkResult.RequiredLangVersion}. Migration will update:");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  • Directory.Build.props: TargetFramework → {TargetFrameworkResult.RequiredTargetFramework}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  • Directory.Build.props: LangVersion → {TargetFrameworkResult.RequiredLangVersion}");
        AnsiConsole.WriteLine();

        return AnsiConsole.Confirm("Do you want to proceed with the upgrade?", defaultValue: true);
    }

    private static string GetRelativeSpecPath(
        string specPath,
        string? apiGeneratedProject)
    {
        if (string.IsNullOrEmpty(apiGeneratedProject))
        {
            return specPath;
        }

        var projectDir = Path.GetDirectoryName(apiGeneratedProject);
        if (string.IsNullOrEmpty(projectDir))
        {
            return specPath;
        }

        try
        {
            var relativePath = Path.GetRelativePath(projectDir, specPath);
            return relativePath.Replace('\\', '/');
        }
        catch
        {
            return specPath;
        }
    }

    private static string GetRelativeSpecPathForClient(
        string clientProject,
        string? serverProject,
        string serverSpecPath)
    {
        if (string.IsNullOrEmpty(serverProject))
        {
            return serverSpecPath;
        }

        var clientDir = Path.GetDirectoryName(clientProject);
        var serverDir = Path.GetDirectoryName(serverProject);

        if (string.IsNullOrEmpty(clientDir) || string.IsNullOrEmpty(serverDir))
        {
            return serverSpecPath;
        }

        // Build path from client to server's spec
        try
        {
            var serverSpecFullPath = Path.GetFullPath(Path.Combine(serverDir, serverSpecPath));
            var relativePath = Path.GetRelativePath(clientDir, serverSpecFullPath);
            return relativePath.Replace('\\', '/');
        }
        catch
        {
            return serverSpecPath;
        }
    }

    private static string GetRelativeSpecPathForDomain(
        string domainProject,
        string? serverProject,
        string serverSpecPath)
        => GetRelativeSpecPathForClient(domainProject, serverProject, serverSpecPath);

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Migration Executor[/]");
        AnsiConsole.WriteLine();
    }
}