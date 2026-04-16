namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// Tests for the options create CLI command.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class OptionsCreateCommandTests : IDisposable
{
    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    private readonly string tempOutputDir;

    public OptionsCreateCommandTests()
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
    public async Task OptionsCreate_Help_DisplaysUsage()
    {
        // Arrange
        var arguments = "options create --help";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful);
        Assert.Contains("--output", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--force", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OptionsCreate_WithValidOutputPath_CreatesOptionsFile()
    {
        // Arrange
        Directory.CreateDirectory(tempOutputDir);
        var arguments = $"options create -o \"{tempOutputDir}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("Created", cleanOutput, StringComparison.Ordinal);

        var optionsFilePath = Path.Combine(tempOutputDir, "ApiGeneratorOptions.json");
        Assert.True(File.Exists(optionsFilePath), $"Options file should exist at {optionsFilePath}");
    }

    [Fact]
    public async Task OptionsCreate_SecondTimeWithoutForce_ReturnsError()
    {
        // Arrange - Create file first
        Directory.CreateDirectory(tempOutputDir);
        var arguments = $"options create -o \"{tempOutputDir}\"";
        await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Act - Try to create again without --force
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful, $"Expected failure but got success. Output: {cleanOutput}");
        Assert.Contains("already exists", cleanOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OptionsCreate_SecondTimeWithForce_Succeeds()
    {
        // Arrange - Create file first
        Directory.CreateDirectory(tempOutputDir);
        var firstArguments = $"options create -o \"{tempOutputDir}\"";
        await ProcessHelper.Execute(
            CliExeFile,
            firstArguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Act - Create again with --force
        var forceArguments = $"options create -o \"{tempOutputDir}\" --force";
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            forceArguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("Created", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OptionsCreate_WithMissingOutputPath_ReturnsError()
    {
        // Arrange
        var arguments = "options create";

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
    public async Task OptionsCreate_DisplaysHeader()
    {
        // Arrange
        Directory.CreateDirectory(tempOutputDir);
        var arguments = $"options create -o \"{tempOutputDir}\"";

        // Act
        var (_, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.Contains("Options File Creator", cleanOutput, StringComparison.Ordinal);
    }
}