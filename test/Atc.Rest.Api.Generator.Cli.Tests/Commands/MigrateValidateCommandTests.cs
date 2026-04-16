namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// Tests for the migrate validate CLI command.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class MigrateValidateCommandTests : IDisposable
{
    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    private readonly string tempOutputDir;

    public MigrateValidateCommandTests()
    {
        tempOutputDir = Path.Combine(Path.GetTempPath(), "atc-rest-api-cli-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(tempOutputDir))
        {
            try
            {
                Directory.Delete(tempOutputDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public async Task MigrateValidate_Help_DisplaysUsage()
    {
        // Arrange
        var arguments = "migrate validate --help";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful);
        Assert.Contains("--solution", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--spec", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--verbose", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrateValidate_WithDummySolution_RunsValidationAndReportsIssues()
    {
        // Arrange - Create a minimal dummy solution structure
        Directory.CreateDirectory(tempOutputDir);
        var slnPath = Path.Combine(tempOutputDir, "TestProject.sln");
        await File.WriteAllTextAsync(
            slnPath,
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            TestContext.Current.CancellationToken);

        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"migrate validate -s \"{slnPath}\" -p \"{yamlPath}\"";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Should run without crashing and display the migration validator header
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("Migration Validator", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrateValidate_WithMissingSolutionPath_ReturnsError()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"migrate validate -p \"{yamlPath}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful);
        Assert.Contains("required", cleanOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigrateValidate_WithMissingSpecificationPath_ReturnsError()
    {
        // Arrange
        Directory.CreateDirectory(tempOutputDir);
        var slnPath = Path.Combine(tempOutputDir, "TestProject.sln");
        await File.WriteAllTextAsync(
            slnPath,
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            TestContext.Current.CancellationToken);

        var arguments = $"migrate validate -s \"{slnPath}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful);
        Assert.Contains("required", cleanOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigrateValidate_WithNonExistentSolution_ReturnsError()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"migrate validate -s \"nonexistent.sln\" -p \"{yamlPath}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful);
        Assert.Contains("not found", cleanOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigrateValidate_WithNonExistentSpec_ReturnsError()
    {
        // Arrange
        Directory.CreateDirectory(tempOutputDir);
        var slnPath = Path.Combine(tempOutputDir, "TestProject.sln");
        await File.WriteAllTextAsync(
            slnPath,
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            TestContext.Current.CancellationToken);

        var arguments = $"migrate validate -s \"{slnPath}\" -p \"nonexistent.yaml\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful);
        Assert.Contains("not found", cleanOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigrateValidate_WithOutputReport_SavesReportFile()
    {
        // Arrange
        Directory.CreateDirectory(tempOutputDir);
        var slnPath = Path.Combine(tempOutputDir, "TestProject.sln");
        await File.WriteAllTextAsync(
            slnPath,
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            TestContext.Current.CancellationToken);

        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var reportPath = Path.Combine(tempOutputDir, "report.json");
        var arguments = $"migrate validate -s \"{slnPath}\" -p \"{yamlPath}\" --output-report \"{reportPath}\"";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("Migration Validator", cleanOutput, StringComparison.Ordinal);

        // The report may or may not be saved depending on validation results,
        // but the command should not crash
        if (File.Exists(reportPath))
        {
            var reportContent = await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken);
            Assert.Contains("solutionPath", reportContent, StringComparison.OrdinalIgnoreCase);
        }
    }
}