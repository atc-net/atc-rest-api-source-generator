namespace Atc.Rest.Api.Generator.Cli.Tests.Services.Migration;

/// <summary>
/// The one migration check that <c>migrate validate</c> and <c>migrate execute</c> both run: the execute command kept its
/// own copy, which had dropped four warnings and left the actual target framework and C# version out of its messages.
/// </summary>
public sealed class MigrationValidatorTests
{
    [Fact]
    public void Validate_AFolderWithoutAnAtcSolution_IsBlockedWithEveryIssueAndWarning()
    {
        var directory = Path.Combine(Path.GetTempPath(), "atc-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var specPath = Path.Combine(directory, "Api.yaml");
            File.WriteAllText(specPath, "openapi: 3.0.3\ninfo:\n  title: Api\n  version: 1.0.0\npaths: {}\n");
            var settings = new MigrateValidateCommandSettings { SolutionPath = directory, SpecificationPath = specPath };
            var progress = new List<string>();

            var report = MigrationValidator.Validate(settings, progress.Add);

            Assert.Equal(MigrationValidationStatus.Blocked, report.Status);
            Assert.Contains("No solution file (.sln/.slnx) found in path", report.BlockingIssues, StringComparer.Ordinal);
            Assert.Contains("No Api.Generated project found. Is this an ATC-generated API?", report.BlockingIssues, StringComparer.Ordinal);
            Assert.Contains("No Domain project found. Handler preservation not possible.", report.Warnings, StringComparer.Ordinal);
            Assert.Contains("No Atc.Rest packages found. This may not be an ATC-generated project.", report.Warnings, StringComparer.Ordinal);
            Assert.Contains("Analyzing project structure...", progress, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}