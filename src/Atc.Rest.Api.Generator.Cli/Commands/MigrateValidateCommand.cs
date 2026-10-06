namespace Atc.Rest.Api.Generator.Cli.Commands;

/// <summary>
/// Command to validate an old-generated API project for migration to source generators.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "CLI needs graceful error handling.")]
public sealed class MigrateValidateCommand : Command<MigrateValidateCommandSettings>
{
    /// <summary>
    /// Analyzes the old-generated solution, prints the migration readiness report and optionally saves it as JSON.
    /// </summary>
    /// <param name="context">The Spectre.Console command context.</param>
    /// <param name="settings">The parsed command-line settings.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>0 when the solution can be migrated; otherwise 1.</returns>
    protected override int Execute(
        CommandContext context,
        MigrateValidateCommandSettings settings,
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

        AnsiConsole.WriteLine();

        return AnsiConsole
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .Start("Validating project for migration...", ctx =>
            {
                try
                {
                    var report = RunValidation(settings, ctx);
                    DisplayResults(report, settings.Verbose);

                    // Save report if requested
                    if (!string.IsNullOrWhiteSpace(settings.OutputReportPath))
                    {
                        SaveReport(report, settings.OutputReportPath);
                    }

                    return report.CanMigrate ? 0 : 1;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[red]✗[/] Error: {ex.Message}");
                    return 1;
                }
            });
    }

    private static MigrationValidationReport RunValidation(
        MigrateValidateCommandSettings settings,
        StatusContext ctx)
    {
        var report = new MigrationValidationReport
        {
            SolutionPath = settings.SolutionPath,
            SpecificationPath = settings.SpecificationPath,
        };

        var rootDirectory = settings.GetSolutionDirectory();

        // 1. Validate project structure
        ctx.Status("Analyzing project structure...");
        report.ProjectStructure = ProjectStructureValidator.Validate(settings.SolutionPath);

        if (!report.ProjectStructure.HasSolutionFile)
        {
            report.BlockingIssues.Add("No solution file (.sln/.slnx) found in path");
        }

        if (!report.ProjectStructure.HasApiGeneratedProject)
        {
            report.BlockingIssues.Add("No Api.Generated project found. Is this an ATC-generated API?");
        }

        if (!report.ProjectStructure.HasDomainProject)
        {
            report.Warnings.Add("No Domain project found. Handler preservation not possible.");
        }

        report.DetectedProjectName = report.ProjectStructure.DetectedProjectName;
        report.DetectedNamespace = report.ProjectStructure.DetectedProjectName;

        // 2. Validate OpenAPI specification
        ctx.Status("Validating OpenAPI specification...");
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

        // 3. Analyze generator options
        ctx.Status("Analyzing generator options...");
        report.GeneratorOptions = GeneratorOptionsAnalyzer.Analyze(rootDirectory);

        if (report.GeneratorOptions.IsControllerBased)
        {
            report.Warnings.Add("Controller-based APIs require manual migration steps");
        }

        // 4. Analyze package references
        ctx.Status("Analyzing package references...");
        report.PackageReferences = PackageReferenceAnalyzer.Analyze(report.ProjectStructure.AllProjects);

        if (!report.PackageReferences.IsAtcGeneratedProject)
        {
            report.Warnings.Add("No Atc.Rest packages found. This may not be an ATC-generated project.");
        }

        // 5. Analyze generated code
        ctx.Status("Analyzing generated code...");
        report.GeneratedCode = GeneratedCodeAnalyzer.Analyze(
            report.ProjectStructure.ApiGeneratedProject,
            report.ProjectStructure.ApiClientGeneratedProject);

        // 6. Analyze handlers
        ctx.Status("Analyzing handler implementations...");
        report.Handlers = HandlerAnalyzer.Analyze(
            report.ProjectStructure.DomainProject,
            report.GeneratedCode.HandlerInterfaces);

        // 7. Validate target framework
        ctx.Status("Validating target framework...");
        report.TargetFramework = TargetFrameworkValidator.Validate(
            rootDirectory,
            report.ProjectStructure.AllProjects);

        if (report.TargetFramework.IsBlocked)
        {
            var tfm = report.TargetFramework.CurrentTargetFramework ?? "unknown";
            var lang = report.TargetFramework.CurrentLangVersion ?? "unknown";

            if (!report.TargetFramework.IsTargetFrameworkCompatible)
            {
                report.BlockingIssues.Add($"Target framework {tfm} not supported. Minimum: net8.0");
            }

            if (!report.TargetFramework.IsLangVersionCompatible)
            {
                report.BlockingIssues.Add($"Language version C# {lang} not supported. Minimum: C# 12");
            }
        }

        report.RequiresUpgrade = report.TargetFramework.RequiresAnyUpgrade;

        // Determine overall status
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

    private static void DisplayResults(
        MigrationValidationReport report,
        bool verbose)
    {
        AnsiConsole.WriteLine();

        // Project Structure
        AnsiConsole.MarkupLine("[blue]Project Structure[/]");
        DisplayCheck(report.ProjectStructure.HasSolutionFile, $"Solution file found: {Path.GetFileName(report.ProjectStructure.SolutionFile ?? "N/A")}");
        DisplayCheck(report.ProjectStructure.HasApiGeneratedProject, $"Api.Generated project found: {GetProjectDisplayName(report.ProjectStructure.ApiGeneratedProject)}");
        DisplayCheck(report.ProjectStructure.HasApiClientGeneratedProject, $"ApiClient.Generated project found: {GetProjectDisplayName(report.ProjectStructure.ApiClientGeneratedProject)}", optional: true);
        DisplayCheck(report.ProjectStructure.HasDomainProject, $"Domain project found: {GetProjectDisplayName(report.ProjectStructure.DomainProject)}", optional: true);
        DisplayCheck(report.ProjectStructure.HasHostApiProject, $"Host API project found: {GetProjectDisplayName(report.ProjectStructure.HostApiProject)}");
        AnsiConsole.WriteLine();

        // OpenAPI Specification
        AnsiConsole.MarkupLine("[blue]OpenAPI Specification[/]");
        DisplayCheck(report.Specification.FileExists, "Specification file exists");
        DisplayCheck(report.Specification.IsValid, $"Valid OpenAPI {report.Specification.OpenApiVersion ?? "3.x"} document");
        if (report.Specification.IsValid)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] Title: {report.Specification.ApiTitle ?? "N/A"}");
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] Endpoints: {report.Specification.OperationCount} operations");
            if (report.Specification.HasMultiPartFiles)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] Multi-part files detected: {report.Specification.MultiPartFiles.Count}");
            }
            else
            {
                AnsiConsole.MarkupLine("  [dim]•[/] No multi-part specifications detected");
            }
        }

        AnsiConsole.WriteLine();

        // Generator Configuration
        AnsiConsole.MarkupLine("[blue]Generator Configuration[/]");
        if (report.GeneratorOptions.Found)
        {
            DisplayCheck(true, $"Options file found: {Path.GetFileName(report.GeneratorOptions.FilePath)}");
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] Output type: {report.GeneratorOptions.AspNetOutputType ?? "MinimalApi"}");
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] Problem details: {(report.GeneratorOptions.UseProblemDetails == true ? "Enabled" : "Disabled")}");
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] Strict mode: {(report.GeneratorOptions.StrictMode == true ? "Enabled" : "Disabled")}");
        }
        else
        {
            AnsiConsole.MarkupLine("  [dim]•[/] No options file found (will use defaults)");
        }

        AnsiConsole.WriteLine();

        // Package References
        AnsiConsole.MarkupLine("[blue]Package References[/]");
        DisplayCheck(report.PackageReferences.HasAtcPackages, $"Atc packages detected (version {report.PackageReferences.AtcVersion ?? "unknown"})");
        if (!string.IsNullOrEmpty(report.PackageReferences.AtcRestMinimalApiVersion))
        {
            DisplayCheck(true, $"Atc.Rest.MinimalApi detected (version {report.PackageReferences.AtcRestMinimalApiVersion})");
        }

        if (!string.IsNullOrEmpty(report.PackageReferences.AtcRestClientVersion))
        {
            DisplayCheck(true, $"Atc.Rest.Client detected (version {report.PackageReferences.AtcRestClientVersion})");
        }

        AnsiConsole.WriteLine();

        // Generated Code Analysis
        AnsiConsole.MarkupLine("[blue]Generated Code Analysis[/]");
        DisplayCheck(report.GeneratedCode.HasGeneratedCode, $"Generated files detected: {report.GeneratedCode.ServerFileCount} server, {report.GeneratedCode.ClientFileCount} client");
        if (!string.IsNullOrEmpty(report.GeneratedCode.GeneratorVersion))
        {
            DisplayCheck(true, $"Generator version: {report.GeneratedCode.GeneratorVersion}");
        }

        if (report.GeneratedCode.HasApiContractMarker || report.GeneratedCode.HasDomainMarker)
        {
            var markers = new List<string>();
            if (report.GeneratedCode.HasApiContractMarker)
            {
                markers.Add("IApiContractAssemblyMarker");
            }

            if (report.GeneratedCode.HasDomainMarker)
            {
                markers.Add("IDomainAssemblyMarker");
            }

            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] Assembly markers: {string.Join(", ", markers)}");
        }

        AnsiConsole.WriteLine();

        // Handler Analysis
        AnsiConsole.MarkupLine("[blue]Handler Analysis[/]");
        DisplayCheck(report.Handlers.HasHandlers, $"Handler implementations found: {report.Handlers.HandlerCount}");
        if (verbose && report.Handlers.HasHandlers)
        {
            foreach (var handler in report.Handlers.Handlers)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"    [dim]•[/] {handler.ClassName} ({handler.ResourceGroup ?? "unknown"})");
            }
        }

        AnsiConsole.WriteLine();

        // Target Framework
        AnsiConsole.MarkupLine("[blue]Target Framework & Language Version[/]");
        var tfmStatus = report.TargetFramework.RequiresTargetFrameworkUpgrade
            ? $"(supported, upgrade required → {TargetFrameworkResult.RequiredTargetFramework})"
            : "(compatible)";
        var langStatus = report.TargetFramework.RequiresLangVersionUpgrade
            ? $"(supported, upgrade required → C# {TargetFrameworkResult.RequiredLangVersion})"
            : "(compatible)";

        DisplayCheck(
            report.TargetFramework.IsTargetFrameworkCompatible,
            $"Target framework: {report.TargetFramework.CurrentTargetFramework ?? "unknown"} {tfmStatus}");
        DisplayCheck(
            report.TargetFramework.IsLangVersionCompatible,
            $"Language version: C# {report.TargetFramework.CurrentLangVersion ?? "unknown"} {langStatus}");

        if (report.TargetFramework.RequiresAnyUpgrade)
        {
            AnsiConsole.MarkupLine("  [yellow]![/] Upgrade to .NET 10 / C# 14 will be required");
        }

        AnsiConsole.WriteLine();

        // Summary
        AnsiConsole.Write(new Rule().RuleStyle(Style.Parse("dim")));

        switch (report.Status)
        {
            case MigrationValidationStatus.Ready:
                AnsiConsole.MarkupLine("[green]✓ Project is eligible for migration[/]");
                break;
            case MigrationValidationStatus.RequiresUpgrade:
                AnsiConsole.MarkupLine("[yellow]! Project is eligible for migration (with .NET 10 / C# 14 upgrade)[/]");
                break;
            case MigrationValidationStatus.Blocked:
                AnsiConsole.MarkupLine("[red]✗ Project cannot be migrated[/]");
                break;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Detected configuration:[/]");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] Project name: {report.DetectedProjectName ?? "unknown"}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] Namespace root: {report.DetectedNamespace ?? "unknown"}");
        var problemDetailsText = report.GeneratorOptions.UseProblemDetails == true ? " with ProblemDetails" : string.Empty;
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] Generation style: {report.GeneratorOptions.AspNetOutputType ?? "MinimalApi"}{problemDetailsText}");
        AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] Handler count: {report.Handlers.HandlerCount}");

        // Display blocking issues
        if (report.BlockingIssues.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Blocking issues:[/]");
            foreach (var issue in report.BlockingIssues)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red]✗[/] {issue}");
            }
        }

        // Display warnings
        if (report.Warnings.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Warnings:[/]");
            foreach (var warning in report.Warnings)
            {
                AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [yellow]![/] {warning}");
            }
        }

        if (report.CanMigrate)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Run 'atc-rest-api-gen migrate execute -s <spec-path> --solution <solution-path>' to perform migration.[/]");
        }

        AnsiConsole.Write(new Rule().RuleStyle(Style.Parse("dim")));
    }

    private static void DisplayCheck(
        bool success,
        string message,
        bool optional = false)
    {
        if (success)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [green]✓[/] {message}");
        }
        else if (optional)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [dim]•[/] {message}");
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"  [red]✗[/] {message}");
        }
    }

    private static string GetProjectDisplayName(string? projectPath)
    {
        if (string.IsNullOrEmpty(projectPath))
        {
            return "N/A";
        }

        return Path.GetFileNameWithoutExtension(projectPath);
    }

    private static void SaveReport(
        MigrationValidationReport report,
        string outputPath)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            var json = JsonSerializer.Serialize(report, options);
            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, json);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[green]✓[/] Report saved to: {fullPath}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated(CultureInfo.InvariantCulture, $"[yellow]![/] Failed to save report: {ex.Message}");
        }
    }

    private static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("ATC REST API").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Migration Validator[/]");
        AnsiConsole.WriteLine();
    }
}