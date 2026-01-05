namespace Atc.Rest.Api.CliGenerator.Tests.Commands;

/// <summary>
/// Tests for the generate client CLI command.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class GenerateClientCommandTests : IDisposable
{
    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    private readonly string tempOutputDir;

    public GenerateClientCommandTests()
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
    public async Task GenerateClient_FirstTime_CreatesAllRequiredFiles()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoFull", "Client");
        var projectName = "Demo.ClientApp";
        var arguments = $"generate client -s \"{yamlPath}\" -o \"{outputPath}\" -n \"{projectName}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("Client project setup completed successfully", cleanOutput, StringComparison.Ordinal);

        // Verify YAML file was copied
        var copiedYamlPath = Path.Combine(outputPath, "Demo.yaml");
        Assert.True(File.Exists(copiedYamlPath), $"YAML file should be copied to {copiedYamlPath}");
        Assert.Contains("Copied specification file", cleanOutput, StringComparison.Ordinal);

        // Verify marker file was created
        var markerFilePath = Path.Combine(outputPath, ".atc-rest-api-client-contracts");
        Assert.True(File.Exists(markerFilePath), $"Marker file should exist at {markerFilePath}");

        // Verify marker file content (now uses ClientConfig with camelCase enum values)
        var markerContent = await File.ReadAllTextAsync(markerFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("\"generate\": true", markerContent, StringComparison.Ordinal);
        Assert.Contains("\"validateSpecificationStrategy\": \"strict\"", markerContent, StringComparison.Ordinal);
        Assert.Contains("\"includeDeprecated\": false", markerContent, StringComparison.Ordinal);
        Assert.Contains("\"clientSuffix\": \"Client\"", markerContent, StringComparison.Ordinal);

        // Verify csproj was created
        var csprojPath = Path.Combine(outputPath, $"{projectName}.csproj");
        Assert.True(File.Exists(csprojPath), $"Project file should exist at {csprojPath}");

        // Verify csproj content references local YAML file (not relative path)
        var csprojContent = await File.ReadAllTextAsync(csprojPath, TestContext.Current.CancellationToken);
        Assert.Contains("<OutputType>Exe</OutputType>", csprojContent, StringComparison.Ordinal);
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", csprojContent, StringComparison.Ordinal);
        Assert.Contains("<AdditionalFiles Include=\"Demo.yaml\"", csprojContent, StringComparison.Ordinal);
        Assert.Contains(".atc-rest-api-client-contracts", csprojContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClient_SecondTime_DoesNotOverwriteExistingFiles()
    {
        // Arrange - Run first time
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoFull2", "Client");
        var projectName = "Demo.ClientApp";
        var arguments = $"generate client -s \"{yamlPath}\" -o \"{outputPath}\" -n \"{projectName}\"";

        // First run
        await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Get file timestamps
        var markerFilePath = Path.Combine(outputPath, ".atc-rest-api-client-contracts");
        var csprojPath = Path.Combine(outputPath, $"{projectName}.csproj");
        var copiedYamlPath = Path.Combine(outputPath, "Demo.yaml");
        var markerWriteTime = File.GetLastWriteTimeUtc(markerFilePath);
        var csprojWriteTime = File.GetLastWriteTimeUtc(csprojPath);
        var yamlWriteTime = File.GetLastWriteTimeUtc(copiedYamlPath);

        // Wait a bit to ensure different timestamp
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Act - Run second time
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        // Files should not have been modified
        Assert.Equal(markerWriteTime, File.GetLastWriteTimeUtc(markerFilePath));
        Assert.Equal(yamlWriteTime, File.GetLastWriteTimeUtc(copiedYamlPath));
        Assert.Contains("Marker file up to date", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Project file up to date", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Specification file up to date", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClient_WithConflictingCsprojName_ReturnsError()
    {
        // Arrange - Create existing project with different name
        var outputPath = Path.Combine(tempOutputDir, "ConflictTest");
        Directory.CreateDirectory(outputPath);
        var existingCsprojPath = Path.Combine(outputPath, "ExistingProject.csproj");
        await File.WriteAllTextAsync(existingCsprojPath, "<Project></Project>", TestContext.Current.CancellationToken);

        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var projectName = "Demo.ClientApp";
        var arguments = $"generate client -s \"{yamlPath}\" -o \"{outputPath}\" -n \"{projectName}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful, "Expected failure due to conflicting project file");
        Assert.Contains("ExistingProject.csproj", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("different project file", cleanOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateClient_WithInvalidYaml_ReturnsValidationError()
    {
        // Arrange - Create an invalid YAML file
        var invalidYamlPath = Path.Combine(tempOutputDir, "invalid.yaml");
        Directory.CreateDirectory(tempOutputDir);
        await File.WriteAllTextAsync(invalidYamlPath, "invalid: yaml: content:", TestContext.Current.CancellationToken);

        var outputPath = Path.Combine(tempOutputDir, "InvalidYamlOutput");
        var projectName = "Test.ClientApp";
        var arguments = $"generate client -s \"{invalidYamlPath}\" -o \"{outputPath}\" -n \"{projectName}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful, $"Expected failure due to invalid YAML. Output: {cleanOutput}");
    }

    [Fact]
    public async Task GenerateClient_WithMissingSpecification_ReturnsError()
    {
        // Arrange
        var arguments = "generate client -o \"output\" -n \"Test.ClientApp\"";

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
    public async Task GenerateClient_WithMissingOutput_ReturnsError()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"generate client -s \"{yamlPath}\" -n \"Test.ClientApp\"";

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
    public async Task GenerateClient_WithMissingProjectName_ReturnsError()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "Output");
        var arguments = $"generate client -s \"{yamlPath}\" -o \"{outputPath}\"";

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
    public async Task GenerateClient_WithProjectNameContainingSpaces_ReturnsError()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "Output");
        var arguments = $"generate client -s \"{yamlPath}\" -o \"{outputPath}\" -n \"Test Client App\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful);
        Assert.Contains("spaces", cleanOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateClient_DisplaysHeader()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "HeaderTest");
        var projectName = "Demo.ClientApp";
        var arguments = $"generate client -s \"{yamlPath}\" -o \"{outputPath}\" -n \"{projectName}\"";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("Client Project Generator", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClient_DisplaysSpecificationAndOutputPaths()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "PathTest");
        var projectName = "Demo.ClientApp";
        var arguments = $"generate client -s \"{yamlPath}\" -o \"{outputPath}\" -n \"{projectName}\"";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("Specification:", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Output path:", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Project name:", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Demo.ClientApp", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClient_Help_DisplaysUsage()
    {
        // Arrange
        var arguments = "generate client --help";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful);
        Assert.Contains("--specification", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--output", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--name", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--no-strict", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClient_WithDefaultMode_ValidatesWithStrictMode()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "StrictTest");
        var projectName = "Demo.ClientApp";
        var arguments = $"generate client -s \"{yamlPath}\" -o \"{outputPath}\" -n \"{projectName}\"";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("Validation mode:", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Strict", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClient_WithNoStrictMode_ValidatesWithStandardMode()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "NoStrictTest");
        var projectName = "Demo.ClientApp";
        var arguments = $"generate client -s \"{yamlPath}\" -o \"{outputPath}\" -n \"{projectName}\" --no-strict";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("Validation mode:", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Standard", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClient_WithNonExistentSpecification_ReturnsError()
    {
        // Arrange
        var outputPath = Path.Combine(tempOutputDir, "NonExistentTest");
        var arguments = $"generate client -s \"nonexistent.yaml\" -o \"{outputPath}\" -n \"Test.ClientApp\"";

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
    public async Task GenerateClient_CreatesOutputDirectoryIfNotExists()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "NewDir", "SubDir", "Client");
        var projectName = "Demo.ClientApp";
        var arguments = $"generate client -s \"{yamlPath}\" -o \"{outputPath}\" -n \"{projectName}\"";

        // Verify directory doesn't exist
        Assert.False(Directory.Exists(outputPath));

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.True(Directory.Exists(outputPath), "Output directory should have been created");
    }
}