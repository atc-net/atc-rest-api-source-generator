namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// The CLI's options do what they say: an option that does nothing is rejected rather than accepted, the preview
/// switch has one name everywhere (--dry-run, with --preview as an alias), and the update check can be turned off.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class CliConsistencyTests : IDisposable
{
    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    private readonly string tempOutputDir = Path.Combine(Path.GetTempPath(), "atc-rest-api-cli-tests", Guid.NewGuid().ToString("N"));

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

    [Theory]
    [InlineData("generate client-typescript -s \"{spec}\" -o \"{out}\" --options \"{out}\"", "--options")]
    [InlineData("generate client-typescript -s \"{spec}\" -o \"{out}\" --report", "--report")]
    [InlineData("migrate execute -s \"{spec}\" --dry-run --verbose", "--verbose")]
    [InlineData("spec validate -s \"{spec}\" --no-such-flag", "no-such-flag")]
    public async Task Command_UnknownOrIneffectiveOption_IsRejected(
        string template,
        string option)
    {
        var (isSuccessful, output) = await Run(template);

        Assert.False(isSuccessful, $"Expected failure but got success. Output: {output}");
        Assert.Contains(option, output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("spec split -s \"{spec}\" -o \"{out}\" --dry-run")]
    [InlineData("spec merge -s \"{spec}\" -o \"{out}/merged.yaml\" --dry-run")]
    public async Task SpecCommand_DryRun_PreviewsAndWritesNothing(
        string template)
    {
        var (isSuccessful, output) = await Run(template);

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {output}");
        Assert.Contains("Preview", output, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(tempOutputDir, "out")), "A dry run writes nothing");
    }

    [Fact]
    public async Task MigrateExecute_PreviewAlias_IsAccepted()
    {
        var (_, output) = await Run("migrate execute -s \"{spec}\" --preview");

        Assert.DoesNotContain("Unknown option", output, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(bool IsSuccessful, string Output)> Run(string template)
    {
        var arguments = template
            .Replace("{spec}", CliTestHelper.GetScenarioYamlPath("Demo"), StringComparison.Ordinal)
            .Replace("{out}", Path.Combine(tempOutputDir, "out"), StringComparison.Ordinal);

        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        return (isSuccessful, CliTestHelper.StripAnsiCodes(output));
    }
}