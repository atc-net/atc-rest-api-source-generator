namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// Tests for the spec validate CLI command.
/// </summary>
[Collection("Sequential-CLI")]
public sealed class SpecValidateCommandTests
{
    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    /// <summary>
    /// Provides all available scenario YAML files for parameterized tests.
    /// Only includes base scenario files where folder name matches file name.
    /// Excludes part files (e.g., MultipartDemo_Users.yaml) and merged files.
    /// </summary>
    public static IEnumerable<object[]> AllScenarios
        => Directory
            .GetFiles(CliTestHelper.GetScenariosPath(), "*.yaml", SearchOption.AllDirectories)
            .Where(path =>
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var folderName = Path.GetFileName(Path.GetDirectoryName(path));
                return string.Equals(fileName, folderName, StringComparison.OrdinalIgnoreCase);
            })
            .Select(path => new object[] { Path.GetFileNameWithoutExtension(path) });

    [Theory]
    [MemberData(nameof(AllScenarios))]
    public async Task SpecValidate_WithDefaultStrictMode_ExecutesWithoutCrashing(
        string scenarioName)
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath(scenarioName);
        var arguments = $"spec validate -s \"{yamlPath}\"";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);

        // Strict mode may return warnings but should not crash
        Assert.Contains("Validation", cleanOutput, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AllScenarios))]
    public async Task SpecValidate_WithNoStrictMode_ReturnsSuccess(
        string scenarioName)
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath(scenarioName);
        var arguments = $"spec validate -s \"{yamlPath}\" --no-strict";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("Validation passed", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecValidate_WithNonExistentFile_ReturnsError()
    {
        // Arrange
        var arguments = "spec validate -s \"nonexistent.yaml\"";

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
    public async Task SpecValidate_WithMissingSpecificationPath_ReturnsError()
    {
        // Arrange
        var arguments = "spec validate";

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
    public async Task SpecValidate_WithInvalidFileExtension_ReturnsError()
    {
        // Arrange
        // Create a temporary file with invalid extension
        var tempFile = Path.GetTempFileName();
        var invalidFile = Path.ChangeExtension(tempFile, ".txt");
        File.Move(tempFile, invalidFile);

        try
        {
            var arguments = $"spec validate -s \"{invalidFile}\"";

            // Act
            var (isSuccessful, output) = await ProcessHelper.Execute(
                CliExeFile,
                arguments,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            var cleanOutput = CliTestHelper.StripAnsiCodes(output);
            Assert.False(isSuccessful);
            Assert.Contains("YAML", cleanOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(invalidFile);
        }
    }

    [Fact]
    public async Task SpecValidate_DisplaysHeader()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("PetStoreSimple");
        var arguments = $"spec validate -s \"{yamlPath}\"";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);

        // FigletText displays ASCII art, check for distinctive patterns
        Assert.Contains("OpenAPI Specification Validator", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecValidate_DisplaysSpecificationPath()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("PetStoreSimple");
        var arguments = $"spec validate -s \"{yamlPath}\"";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("Specification:", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("PetStoreSimple.yaml", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecValidate_WithDefaultMode_DisplaysStrictValidationMode()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("PetStoreSimple");
        var arguments = $"spec validate -s \"{yamlPath}\"";

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
    public async Task SpecValidate_WithNoStrictMode_DisplaysStandardValidationMode()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("PetStoreSimple");
        var arguments = $"spec validate -s \"{yamlPath}\" --no-strict";

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
    public async Task SpecValidate_Help_DisplaysUsage()
    {
        // Arrange
        var arguments = "spec validate --help";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful);
        Assert.Contains("--specification", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--no-strict", cleanOutput, StringComparison.Ordinal);
    }
}