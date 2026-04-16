namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// Tests for the spec split CLI command.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class SpecSplitCommandTests : IDisposable
{
    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    private readonly string tempOutputDir;

    public SpecSplitCommandTests()
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
    public async Task SpecSplit_Help_DisplaysUsage()
    {
        // Arrange
        var arguments = "spec split --help";

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
        Assert.Contains("--strategy", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--preview", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecSplit_WithPreviewMode_DisplaysSplitResult()
    {
        // Arrange - Use the Demo scenario for splitting
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"spec split -s \"{yamlPath}\" --preview";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("Preview", cleanOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SpecSplit_WithOutputPath_WritesSplitFiles()
    {
        // Arrange
        Directory.CreateDirectory(tempOutputDir);
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "split-output");
        var arguments = $"spec split -s \"{yamlPath}\" -o \"{outputPath}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.True(Directory.Exists(outputPath), $"Split output directory should exist at {outputPath}");
        Assert.Contains("Split specification written to", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecSplit_WithMissingSpecification_ReturnsError()
    {
        // Arrange
        var arguments = "spec split";

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
    public async Task SpecSplit_WithNonExistentFile_ReturnsError()
    {
        // Arrange
        var arguments = "spec split -s \"nonexistent.yaml\" --preview";

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
    public async Task SpecSplit_DisplaysHeader()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"spec split -s \"{yamlPath}\" --preview";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("OpenAPI Specification Split Tool", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecSplit_WithByPathSegmentStrategy_Succeeds()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"spec split -s \"{yamlPath}\" --preview --strategy ByPathSegment";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
    }
}