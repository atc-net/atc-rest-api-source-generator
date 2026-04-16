namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// Tests for the migrate execute CLI command.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class MigrateExecuteCommandTests : IDisposable
{
    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    private readonly string tempOutputDir;

    public MigrateExecuteCommandTests()
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
    public async Task MigrateExecute_Help_DisplaysUsage()
    {
        // Arrange
        var arguments = "migrate execute --help";

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
        Assert.Contains("--dry-run", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--force", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrateExecute_WithDummySolutionDryRun_ReportsCannotMigrate()
    {
        // Arrange - Create a minimal dummy solution structure (no Api.Generated project)
        Directory.CreateDirectory(tempOutputDir);
        var slnPath = Path.Combine(tempOutputDir, "TestProject.sln");
        await File.WriteAllTextAsync(
            slnPath,
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            TestContext.Current.CancellationToken);

        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"migrate execute -s \"{slnPath}\" -p \"{yamlPath}\" --dry-run --force";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Should fail validation (no Api.Generated project) but not crash
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful, $"Expected failure because dummy solution lacks required projects. Output: {cleanOutput}");
        Assert.Contains("cannot be migrated", cleanOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigrateExecute_WithMissingSolutionPath_ReturnsError()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"migrate execute -p \"{yamlPath}\"";

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
    public async Task MigrateExecute_WithMissingSpecificationPath_ReturnsError()
    {
        // Arrange
        Directory.CreateDirectory(tempOutputDir);
        var slnPath = Path.Combine(tempOutputDir, "TestProject.sln");
        await File.WriteAllTextAsync(
            slnPath,
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            TestContext.Current.CancellationToken);

        var arguments = $"migrate execute -s \"{slnPath}\"";

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
    public async Task MigrateExecute_WithNonExistentSolution_ReturnsError()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"migrate execute -s \"nonexistent.sln\" -p \"{yamlPath}\"";

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
    public async Task MigrateExecute_WithNonExistentSpec_ReturnsError()
    {
        // Arrange
        Directory.CreateDirectory(tempOutputDir);
        var slnPath = Path.Combine(tempOutputDir, "TestProject.sln");
        await File.WriteAllTextAsync(
            slnPath,
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            TestContext.Current.CancellationToken);

        var arguments = $"migrate execute -s \"{slnPath}\" -p \"nonexistent.yaml\"";

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
    public async Task MigrateExecute_DisplaysHeader()
    {
        // Arrange
        Directory.CreateDirectory(tempOutputDir);
        var slnPath = Path.Combine(tempOutputDir, "TestProject.sln");
        await File.WriteAllTextAsync(
            slnPath,
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            TestContext.Current.CancellationToken);

        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"migrate execute -s \"{slnPath}\" -p \"{yamlPath}\" --dry-run --force";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("Migration Executor", cleanOutput, StringComparison.Ordinal);
    }
}