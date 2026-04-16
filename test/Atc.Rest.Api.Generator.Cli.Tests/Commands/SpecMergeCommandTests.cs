namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// Tests for the spec merge CLI command.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class SpecMergeCommandTests : IDisposable
{
    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    private readonly string tempOutputDir;

    public SpecMergeCommandTests()
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
    public async Task SpecMerge_Help_DisplaysUsage()
    {
        // Arrange
        var arguments = "spec merge --help";

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
        Assert.Contains("--preview", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--validate", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecMerge_WithPreviewMode_DisplaysMergeResult()
    {
        // Arrange - Use the MultiParts scenario which has base + part files
        var yamlPath = CliTestHelper.GetScenarioYamlPath("MultiParts");
        var arguments = $"spec merge -s \"{yamlPath}\" --preview";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("Preview", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Files to merge", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecMerge_WithOutputPath_WritesMergedFile()
    {
        // Arrange
        Directory.CreateDirectory(tempOutputDir);
        var yamlPath = CliTestHelper.GetScenarioYamlPath("MultiParts");
        var outputPath = Path.Combine(tempOutputDir, "merged.yaml");
        var arguments = $"spec merge -s \"{yamlPath}\" -o \"{outputPath}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.True(File.Exists(outputPath), $"Merged file should exist at {outputPath}");
        Assert.Contains("Merged specification written to", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecMerge_WithMissingSpecificationAndFiles_ReturnsError()
    {
        // Arrange
        var arguments = "spec merge";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful);
        Assert.Contains("specification path", cleanOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SpecMerge_WithNonExistentFile_ReturnsError()
    {
        // Arrange
        var arguments = "spec merge -s \"nonexistent.yaml\" --preview";

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
    public async Task SpecMerge_DisplaysHeader()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("MultiParts");
        var arguments = $"spec merge -s \"{yamlPath}\" --preview";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("OpenAPI Specification Merge Tool", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecMerge_WithValidateFlag_RunsValidation()
    {
        // Arrange
        Directory.CreateDirectory(tempOutputDir);
        var yamlPath = CliTestHelper.GetScenarioYamlPath("MultiParts");
        var outputPath = Path.Combine(tempOutputDir, "merged-validated.yaml");
        var arguments = $"spec merge -s \"{yamlPath}\" -o \"{outputPath}\" --validate";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - The --validate flag causes validation to run after merge.
        // The MultiParts scenario may have validation issues, so we just verify
        // the validation step was attempted (not that it passes).
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("Validating merged specification", cleanOutput, StringComparison.Ordinal);
    }
}