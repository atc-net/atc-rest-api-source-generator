namespace Atc.Rest.Api.Generator.Cli.Services.Migration;

/// <summary>
/// The migration check that <c>migrate validate</c> reports and <c>migrate execute</c> runs before it changes anything:
/// the project structure, the specification, the generator options, the packages, the generated code, the handlers and
/// the target framework, and from them the blocking issues, the warnings and the status.
/// </summary>
public static class MigrationValidator
{
    /// <summary>
    /// Validates a project for migration.
    /// </summary>
    /// <param name="settings">The solution and specification to validate.</param>
    /// <param name="progress">Told what is being analyzed, step by step, or <see langword="null"/>.</param>
    /// <returns>The validation report.</returns>
    public static MigrationValidationReport Validate(
        MigrateValidateCommandSettings settings,
        Action<string>? progress = null)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var report = new MigrationValidationReport
        {
            SolutionPath = settings.SolutionPath,
            SpecificationPath = settings.SpecificationPath,
        };

        var rootDirectory = settings.GetSolutionDirectory();

        // 1. Project structure
        progress?.Invoke("Analyzing project structure...");
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

        // 2. OpenAPI specification
        progress?.Invoke("Validating OpenAPI specification...");
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

        // 3. Generator options
        progress?.Invoke("Analyzing generator options...");
        report.GeneratorOptions = GeneratorOptionsAnalyzer.Analyze(rootDirectory);

        if (report.GeneratorOptions.IsControllerBased)
        {
            report.Warnings.Add("Controller-based APIs require manual migration steps");
        }

        // 4. Package references
        progress?.Invoke("Analyzing package references...");
        report.PackageReferences = PackageReferenceAnalyzer.Analyze(report.ProjectStructure.AllProjects);

        if (!report.PackageReferences.IsAtcGeneratedProject)
        {
            report.Warnings.Add("No Atc.Rest packages found. This may not be an ATC-generated project.");
        }

        foreach (var upgrade in report.PackageReferences.PackagesToUpgrade)
        {
            report.Warnings.Add($"{upgrade.Package} {upgrade.Version} is older than the generated code needs: update it to {upgrade.Minimum} or later, or the first build stops with {upgrade.RuleId}.");
        }

        // 5. Generated code
        progress?.Invoke("Analyzing generated code...");
        report.GeneratedCode = GeneratedCodeAnalyzer.Analyze(
            report.ProjectStructure.ApiGeneratedProject,
            report.ProjectStructure.ApiClientGeneratedProject);

        // 6. Handlers
        progress?.Invoke("Analyzing handler implementations...");
        report.Handlers = HandlerAnalyzer.Analyze(
            report.ProjectStructure.DomainProject,
            report.GeneratedCode.HandlerInterfaces);

        // 7. Target framework
        progress?.Invoke("Validating target framework...");
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
}