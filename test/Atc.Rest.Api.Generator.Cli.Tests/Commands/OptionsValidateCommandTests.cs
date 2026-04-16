namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// Tests for the options validate CLI command.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class OptionsValidateCommandTests : IDisposable
{
    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    private readonly string tempOutputDir;

    public OptionsValidateCommandTests()
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
    public async Task OptionsValidate_Help_DisplaysUsage()
    {
        // Arrange
        var arguments = "options validate --help";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful);
        Assert.Contains("--output", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OptionsValidate_WithValidOptionsFile_ReturnsSuccess()
    {
        // Arrange - Create a valid options file first
        Directory.CreateDirectory(tempOutputDir);
        var createArguments = $"options create -o \"{tempOutputDir}\"";
        await ProcessHelper.Execute(
            CliExeFile,
            createArguments,
            cancellationToken: TestContext.Current.CancellationToken);

        var arguments = $"options validate -o \"{tempOutputDir}\"";

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
    public async Task OptionsValidate_WithInvalidJsonContent_ReturnsError()
    {
        // Arrange - Create a file with invalid JSON content
        Directory.CreateDirectory(tempOutputDir);
        var optionsFilePath = Path.Combine(tempOutputDir, "ApiGeneratorOptions.json");
        await File.WriteAllTextAsync(
            optionsFilePath,
            "{ invalid json content }",
            TestContext.Current.CancellationToken);

        var arguments = $"options validate -o \"{tempOutputDir}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful, $"Expected failure but got success. Output: {cleanOutput}");
    }

    [Fact]
    public async Task OptionsValidate_WithMissingOutputPath_ReturnsError()
    {
        // Arrange
        var arguments = "options validate";

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
    public async Task OptionsValidate_DisplaysHeader()
    {
        // Arrange - Create a valid options file first
        Directory.CreateDirectory(tempOutputDir);
        var createArguments = $"options create -o \"{tempOutputDir}\"";
        await ProcessHelper.Execute(
            CliExeFile,
            createArguments,
            cancellationToken: TestContext.Current.CancellationToken);

        var arguments = $"options validate -o \"{tempOutputDir}\"";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("Options File Validator", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OptionsValidate_WithValidFile_DisplaysConfigurationSummary()
    {
        // Arrange - Create a valid options file first
        Directory.CreateDirectory(tempOutputDir);
        var createArguments = $"options create -o \"{tempOutputDir}\"";
        await ProcessHelper.Execute(
            CliExeFile,
            createArguments,
            cancellationToken: TestContext.Current.CancellationToken);

        var arguments = $"options validate -o \"{tempOutputDir}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("Configuration summary", cleanOutput, StringComparison.Ordinal);
    }
}