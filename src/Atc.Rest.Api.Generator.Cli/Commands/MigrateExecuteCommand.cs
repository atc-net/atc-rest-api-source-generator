namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to execute migration from old CLI-generated API to source generators.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class MigrateExecuteCommand : Command<MigrateExecuteCommandSettings>
{
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

        AnsiConsole.MarkupLine($"[blue]Solution:[/]      {Markup.Escape(solutionPath)}");
        AnsiConsole.MarkupLine($"[blue]Specification:[/] {Markup.Escape(specPath)}");
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

            var report = RunValidation(validationSettings);

            if (!report.CanMigrate)
            {
                AnsiConsole.MarkupLine("[red]✗ Project cannot be migrated. Please fix the blocking issues first.[/]");
                foreach (var issue in report.BlockingIssues)
                {
                    AnsiConsole.MarkupLine($"  [red]•[/] {Markup.Escape(issue)}");
                }

                return 1;
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
            return ExecuteMigration(settings, report, rootDirectory, specPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private static int ExecuteMigration(
        MigrateExecuteCommandSettings settings,
        MigrationValidationReport report,
        string rootDirectory,
        string specPath)
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
                var specRelativePath = GetRelativeSpecPath(rootDirectory, specPath, report.ProjectStructure.ApiGeneratedProject);
                summary.SpecificationPath = specRelativePath;

                // Step 5: Generate marker files
                ctx.Status(dryRun ? "Analyzing marker files..." : "Generating marker files...");
                GenerateMarkerFiles(report, projectName, dryRun, summary);

                // Step 6: Modify project files
                ctx.Status(dryRun ? "Analyzing project modifications..." : "Modifying project files...");
                ModifyProjectFiles(
                    report,
                    specRelativePath,
                    PackageVersionDefaults.SourceGeneratorFallback,
                    PackageVersionDefaults.RestClientMinFallback,
                    dryRun,
                    summary);

                // Step 7: Handle ATC coding rules
                ctx.Status(dryRun ? "Analyzing ATC coding rules..." : "Handling ATC coding rules...");
                HandleAtcCodingRules(rootDirectory, settings.Force, dryRun, summary);

                // Step 7b: Upgrade Directory.Build.props if needed
                if (report.RequiresUpgrade)
                {
                    ctx.Status(dryRun ? "Analyzing Directory.Build.props..." : "Upgrading Directory.Build.props...");
                    UpgradeDirectoryBuildProps(rootDirectory, dryRun, summary);
                }

                // Step 8: Clean generated code
                ctx.Status(dryRun ? "Analyzing generated code cleanup..." : "Cleaning generated code...");
                CleanGeneratedCode(report, dryRun, summary);

                // Step 9: Rename projects
                ctx.Status(dryRun ? "Analyzing project renames..." : "Renaming projects...");
                RenameProjects(report, projectName, dryRun, summary);

                // Step 10: Update solution and references
                ctx.Status(dryRun ? "Analyzing solution updates..." : "Updating solution and references...");
                UpdateSolutionAndReferences(report, projectName, dryRun, summary);

                // Step 11: Update GlobalUsings.cs in Domain project
                ctx.Status(dryRun ? "Analyzing GlobalUsings.cs..." : "Updating GlobalUsings.cs...");
                UpdateGlobalUsings(report, projectName, dryRun, summary);

                // Step 12: Update namespace references in Domain code files
                ctx.Status(dryRun ? "Analyzing domain code namespaces..." : "Updating domain code namespaces...");
                UpdateDomainCodeNamespaces(report, projectName, dryRun, summary);

                // Step 13: Update Host project (Program.cs and GlobalUsings)
                ctx.Status(dryRun ? "Analyzing Host project..." : "Updating Host project...");
                UpdateHostProject(report, projectName, dryRun, summary);

                // Display summary
                DisplayMigrationSummary(summary, dryRun);

                if (!dryRun)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[green]✓ Migration completed successfully![/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Next steps:[/]");
                    AnsiConsole.MarkupLine("  1. Run 'dotnet build' to verify the migration");
                    AnsiConsole.MarkupLine("  2. Review the generated code");
                    AnsiConsole.MarkupLine("  3. Commit the changes to git");
                }

                return 0;
            });
    }

    private static void GenerateMarkerFiles(
        MigrationValidationReport report,
        string projectName,
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
        }
    }

    private static void RenameProjects(
        MigrationValidationReport report,
        string projectName,
        bool dryRun,
        MigrationSummary summary)
    {
        // Rename Api.Generated → Api.Contracts
        if (!string.IsNullOrEmpty(report.ProjectStructure.ApiGeneratedProject))
        {
            var serverDir = Path.GetDirectoryName(report.ProjectStructure.ApiGeneratedProject)!;
            var result = ProjectRenamer.RenameServerProject(serverDir, projectName, dryRun);
            if (result.Success)
            {
                summary.RenamedProjects.Add($"{result.OldName} → {result.NewName}");
            }
        }

        // Rename ApiClient.Generated → ApiClient
        if (!string.IsNullOrEmpty(report.ProjectStructure.ApiClientGeneratedProject))
        {
            var clientDir = Path.GetDirectoryName(report.ProjectStructure.ApiClientGeneratedProject)!;
            var result = ProjectRenamer.RenameClientProject(clientDir, projectName, dryRun);
            if (result.Success)
            {
                summary.RenamedProjects.Add($"{result.OldName} → {result.NewName}");
            }
        }
    }

    private static void UpdateSolutionAndReferences(
        MigrationValidationReport report,
        string projectName,
        bool dryRun,
        MigrationSummary summary)
    {
        // Update solution file
        if (!string.IsNullOrEmpty(report.ProjectStructure.SolutionFile))
        {
            var result = SolutionModifier.UpdateAllReferences(
                report.ProjectStructure.SolutionFile,
                projectName,
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

    private static void UpdateGlobalUsings(
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
            ProcessGlobalUsingsForProject(report, domainDir, contractsNamespace, dryRun, summary);
        }

        // Process Domain.Tests project
        if (!string.IsNullOrEmpty(report.ProjectStructure.DomainTestProject))
        {
            var testDir = Path.GetDirectoryName(report.ProjectStructure.DomainTestProject)!;
            ProcessGlobalUsingsForProject(report, testDir, contractsNamespace, dryRun, summary);
        }
    }

    private static void ProcessGlobalUsingsForProject(
        MigrationValidationReport report,
        string projectDir,
        string contractsNamespace,
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

        // Step 3: Add new generated namespace usings based on the OpenAPI spec
        if (!string.IsNullOrEmpty(report.SpecificationPath))
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
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(addResult.Error)}");
            }
        }

        // Step 4: Sort global usings (System first, then by namespace group)
        GlobalUsingsModifier.SortGlobalUsings(projectDir, dryRun);
    }

    private static void UpdateDomainCodeNamespaces(
        MigrationValidationReport report,
        string projectName,
        bool dryRun,
        MigrationSummary summary)
    {
        // Process Domain project
        if (!string.IsNullOrEmpty(report.ProjectStructure.DomainProject))
        {
            var domainDir = Path.GetDirectoryName(report.ProjectStructure.DomainProject)!;
            ProcessCodeNamespacesForProject(domainDir, projectName, dryRun, summary);
        }

        // Process Domain.Tests project
        if (!string.IsNullOrEmpty(report.ProjectStructure.DomainTestProject))
        {
            var testDir = Path.GetDirectoryName(report.ProjectStructure.DomainTestProject)!;
            ProcessCodeNamespacesForProject(testDir, projectName, dryRun, summary);
        }
    }

    private static void ProcessCodeNamespacesForProject(
        string projectDir,
        string projectName,
        bool dryRun,
        MigrationSummary summary)
    {
        var result = DomainCodeNamespaceUpdater.UpdateNamespaceReferences(projectDir, projectName, dryRun);

        if (result.UpdatedFiles.Count > 0)
        {
            summary.DomainCodeUpdates.AddRange(result.UpdatedFiles);
            summary.DomainCodeTotalReplacements += result.TotalReplacements;

            foreach (var file in result.UpdatedFiles)
            {
                if (!summary.ModifiedFiles.Contains(file.FilePath, StringComparer.Ordinal))
                {
                    summary.ModifiedFiles.Add(file.FilePath);
                }
            }
        }

        if (!string.IsNullOrEmpty(result.Error))
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(result.Error)}");
        }
    }

    private static void UpdateHostProject(
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
            dryRun);

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
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(globalUsingsResult.Error)}");
        }

        if (!string.IsNullOrEmpty(programResult.Error))
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(programResult.Error)}");
        }
    }

    private static void UpgradeDirectoryBuildProps(
        string rootDirectory,
        bool dryRun,
        MigrationSummary summary)
    {
        const string targetFramework = "net10.0";
        const string langVersion = "14.0";

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
                AnsiConsole.MarkupLine($"  [green]+[/] {Markup.Escape(file)}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.DeletedFolders.Count > 0 || summary.DeletedFiles.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Files/Folders to Delete[/]");
            foreach (var folder in summary.DeletedFolders)
            {
                AnsiConsole.MarkupLine($"  [red]-[/] {Markup.Escape(folder)}");
            }

            foreach (var file in summary.DeletedFiles)
            {
                AnsiConsole.MarkupLine($"  [red]-[/] {Markup.Escape(file)}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.ModifiedFiles.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Files to Modify[/]");
            foreach (var file in summary.ModifiedFiles.Distinct(StringComparer.Ordinal))
            {
                AnsiConsole.MarkupLine($"  [yellow]~[/] {Markup.Escape(file)}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.RenamedProjects.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Projects to Rename[/]");
            foreach (var rename in summary.RenamedProjects)
            {
                AnsiConsole.MarkupLine($"  [cyan]↳[/] {Markup.Escape(rename)}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.UpdatedReferences.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Project References to Update[/]");
            foreach (var reference in summary.UpdatedReferences)
            {
                AnsiConsole.MarkupLine($"  [cyan]↳[/] {Markup.Escape(reference)}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.UpdatedGlobalUsings.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]GlobalUsings.cs Namespace Updates[/]");
            foreach (var update in summary.UpdatedGlobalUsings)
            {
                AnsiConsole.MarkupLine($"  [cyan]↳[/] {Markup.Escape(update)}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.DirectoryBuildPropsUpgraded)
        {
            AnsiConsole.MarkupLine("[blue]Directory.Build.props Upgrade[/]");
            AnsiConsole.MarkupLine($"  [cyan]↳[/] TargetFramework → {summary.UpgradedTargetFramework}");
            AnsiConsole.MarkupLine($"  [cyan]↳[/] LangVersion → {summary.UpgradedLangVersion}");
            AnsiConsole.WriteLine();
        }

        if (summary.DomainCodeUpdates.Count > 0)
        {
            AnsiConsole.MarkupLine($"[blue]Domain Code Namespace Updates ({summary.DomainCodeTotalReplacements} replacements)[/]");
            foreach (var fileUpdate in summary.DomainCodeUpdates)
            {
                var relativePath = Path.GetFileName(fileUpdate.FilePath);
                AnsiConsole.MarkupLine($"  [yellow]~[/] {Markup.Escape(relativePath)} ({fileUpdate.Replacements.Count} replacements)");
                foreach (var replacement in fileUpdate.Replacements)
                {
                    AnsiConsole.MarkupLine($"    [cyan]↳[/] {Markup.Escape(replacement)}");
                }
            }

            AnsiConsole.WriteLine();
        }

        if (summary.HostProjectUpdates.Count > 0)
        {
            AnsiConsole.MarkupLine("[blue]Host Project Updates[/]");
            foreach (var update in summary.HostProjectUpdates)
            {
                AnsiConsole.MarkupLine($"  [cyan]↳[/] {Markup.Escape(update)}");
            }

            AnsiConsole.WriteLine();
        }

        if (summary.AtcCodingRulesWillPrompt && dryRun)
        {
            AnsiConsole.MarkupLine("[yellow]![/] Will prompt to set up atc-coding-rules-updater");
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine($"[dim]Summary: " +
                               $"{summary.CreatedFiles.Count} create, " +
                               $"{summary.DeletedFolders.Count + summary.DeletedFiles.Count} delete, " +
                               $"{summary.ModifiedFiles.Distinct(StringComparer.Ordinal).Count()} modify, " +
                               $"{summary.RenamedProjects.Count} rename[/]");
    }

    private static MigrationValidationReport RunValidation(
        MigrateValidateCommandSettings settings)
    {
        var report = new MigrationValidationReport
        {
            SolutionPath = settings.SolutionPath,
            SpecificationPath = settings.SpecificationPath,
        };

        var rootDirectory = settings.GetSolutionDirectory();

        // Run all validators
        report.ProjectStructure = ProjectStructureValidator.Validate(settings.SolutionPath);

        if (!report.ProjectStructure.HasSolutionFile)
        {
            report.BlockingIssues.Add("No solution file (.sln/.slnx) found in path");
        }

        if (!report.ProjectStructure.HasApiGeneratedProject)
        {
            report.BlockingIssues.Add("No Api.Generated project found. Is this an ATC-generated API?");
        }

        report.DetectedProjectName = report.ProjectStructure.DetectedProjectName;
        report.DetectedNamespace = report.ProjectStructure.DetectedProjectName;

        report.Specification = SpecificationValidator.Validate(settings.SpecificationPath);

        if (!report.Specification.FileExists)
        {
            report.BlockingIssues.Add($"Specification file not found: {settings.SpecificationPath}");
        }
        else if (!report.Specification.IsValid)
        {
            foreach (var error in report.Specification.ValidationErrors)
            {
                report.BlockingIssues.Add($"OpenAPI validation error: {error}");
            }
        }

        report.GeneratorOptions = GeneratorOptionsAnalyzer.Analyze(rootDirectory);
        report.PackageReferences = PackageReferenceAnalyzer.Analyze(report.ProjectStructure.AllProjects);
        report.GeneratedCode = GeneratedCodeAnalyzer.Analyze(
            report.ProjectStructure.ApiGeneratedProject,
            report.ProjectStructure.ApiClientGeneratedProject);
        report.Handlers = HandlerAnalyzer.Analyze(
            report.ProjectStructure.DomainProject,
            report.GeneratedCode.HandlerInterfaces);
        report.TargetFramework = TargetFrameworkValidator.Validate(rootDirectory, report.ProjectStructure.AllProjects);

        if (report.TargetFramework.IsBlocked)
        {
            if (!report.TargetFramework.IsTargetFrameworkCompatible)
            {
                report.BlockingIssues.Add($"Target framework not supported. Minimum: net8.0");
            }

            if (!report.TargetFramework.IsLangVersionCompatible)
            {
                report.BlockingIssues.Add($"Language version not supported. Minimum: C# 12");
            }
        }

        report.RequiresUpgrade = report.TargetFramework.RequiresAnyUpgrade;

        if (report.BlockingIssues.Count > 0)
        {
            report.Status = MigrationValidationStatus.Blocked;
        }
        else if (report.RequiresUpgrade)
        {
            report.Status = MigrationValidationStatus.RequiresUpgrade;
        }
        else
        {
            report.Status = MigrationValidationStatus.Ready;
        }

        return report;
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
            AnsiConsole.MarkupLine($"[yellow]![/] Git command failed: {gitStatus.ErrorMessage}");
            return true;
        }

        if (gitStatus.HasUncommittedChanges)
        {
            if (dryRun)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ Git status: {gitStatus.UncommittedFiles.Count} uncommitted changes detected[/]");
                AnsiConsole.MarkupLine("  [dim](Use --force to skip this check)[/]");
                return true;
            }

            if (force)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ Git status: {gitStatus.UncommittedFiles.Count} uncommitted changes (--force specified)[/]");
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
        AnsiConsole.MarkupLine($"  • Target framework: {report.TargetFramework.CurrentTargetFramework ?? "unknown"} → net10.0");
        AnsiConsole.MarkupLine($"  • Language version: C# {report.TargetFramework.CurrentLangVersion ?? "unknown"} → C# 14");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("The source generator requires .NET 10 with C# 14. Migration will update:");
        AnsiConsole.MarkupLine("  • Directory.Build.props: TargetFramework → net10.0");
        AnsiConsole.MarkupLine("  • Directory.Build.props: LangVersion → 14.0");
        AnsiConsole.WriteLine();

        return AnsiConsole.Confirm("Do you want to proceed with the upgrade?", defaultValue: true);
    }

    private static string GetRelativeSpecPath(
        string rootDirectory,
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