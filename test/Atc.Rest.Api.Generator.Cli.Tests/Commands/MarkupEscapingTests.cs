namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// Every command prints paths, spec-derived names and exception messages through Spectre markup.
/// A <c>[</c> in any of them must be printed, never parsed as a style tag.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class MarkupEscapingTests : IDisposable
{
    private static readonly string[] CrashMarkers =
    [
        "Unhandled exception",
        "InvalidOperationException",
        "Could not find color or style",
        "Encountered malformed markup",
    ];

    private readonly string tempRoot;
    private readonly string specDirectory;
    private readonly string outputDirectory;
    private readonly string specPath;

    public MarkupEscapingTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "atc-rest-api-cli-tests", Guid.NewGuid().ToString("N"));
        specDirectory = Path.Combine(tempRoot, "work [2026]");
        outputDirectory = Path.Combine(tempRoot, "out]");
        Directory.CreateDirectory(specDirectory);
        Directory.CreateDirectory(outputDirectory);

        specPath = Path.Combine(specDirectory, "Demo.yaml");
        File.Copy(CliTestHelper.GetScenarioYamlPath("Demo"), specPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public async Task GenerateServer_WithBracketsInPaths_Succeeds()
    {
        var output = Path.Combine(outputDirectory, "server");
        var (exitCode, text) = await CliTestHelper.RunCliAsync(
            $"generate server -s \"{specPath}\" -o \"{output}\" -n Demo",
            TestContext.Current.CancellationToken);

        AssertNoCrash(text);
        Assert.Equal(0, exitCode);
        Assert.Contains("work [2026]", text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(output, "src", "Demo.Api.Contracts", ".atc-rest-api-server")));
    }

    [Fact]
    public async Task GenerateClient_WithBracketsInPaths_Succeeds()
    {
        var output = Path.Combine(outputDirectory, "client");
        var (exitCode, text) = await CliTestHelper.RunCliAsync(
            $"generate client -s \"{specPath}\" -o \"{output}\" -n Demo.ApiClient",
            TestContext.Current.CancellationToken);

        AssertNoCrash(text);
        Assert.Equal(0, exitCode);
        Assert.Contains("work [2026]", text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(output, ".atc-rest-api-client")));
    }

    [Fact]
    public async Task GenerateClientTypeScript_WithBracketsInPaths_Succeeds()
    {
        var output = Path.Combine(outputDirectory, "ts");
        var (exitCode, text) = await CliTestHelper.RunCliAsync(
            $"generate client-typescript -s \"{specPath}\" -o \"{output}\"",
            TestContext.Current.CancellationToken);

        AssertNoCrash(text);
        Assert.Equal(0, exitCode);
        Assert.Contains("work [2026]", text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(output, "index.ts")));
    }

    [Fact]
    public async Task SpecValidate_WithBracketsInPath_Succeeds()
    {
        var (exitCode, text) = await CliTestHelper.RunCliAsync(
            $"spec validate -s \"{specPath}\" --no-strict",
            TestContext.Current.CancellationToken);

        AssertNoCrash(text);
        Assert.Equal(0, exitCode);
        Assert.Contains("work [2026]", text, StringComparison.Ordinal);
        Assert.Contains("Validation passed", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecMerge_WithBracketsInPaths_Succeeds()
    {
        var basePath = Path.Combine(specDirectory, "MultiParts.yaml");
        foreach (var part in Directory.GetFiles(Path.Combine(CliTestHelper.GetScenariosPath(), "MultiParts"), "MultiParts*.yaml"))
        {
            File.Copy(part, Path.Combine(specDirectory, Path.GetFileName(part)));
        }

        var output = Path.Combine(outputDirectory, "Merged.yaml");
        var (exitCode, text) = await CliTestHelper.RunCliAsync(
            $"spec merge -s \"{basePath}\" -o \"{output}\"",
            TestContext.Current.CancellationToken);

        AssertNoCrash(text);
        Assert.Equal(0, exitCode);
        Assert.Contains("out]", text, StringComparison.Ordinal);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task SpecSplit_WithBracketsInPaths_Succeeds()
    {
        var output = Path.Combine(outputDirectory, "split");
        var (exitCode, text) = await CliTestHelper.RunCliAsync(
            $"spec split -s \"{specPath}\" -o \"{output}\"",
            TestContext.Current.CancellationToken);

        AssertNoCrash(text);
        Assert.Equal(0, exitCode);
        Assert.Contains("out]", text, StringComparison.Ordinal);
        Assert.True(Directory.Exists(output));
    }

    [Fact]
    public async Task SpecSplit_PreviewWithBracketsInPath_Succeeds()
    {
        var (exitCode, text) = await CliTestHelper.RunCliAsync(
            $"spec split -s \"{specPath}\" --preview",
            TestContext.Current.CancellationToken);

        AssertNoCrash(text);
        Assert.Equal(0, exitCode);
        Assert.Contains("Preview", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OptionsCreateAndValidate_WithBracketsInPath_Succeed()
    {
        var (createExitCode, createText) = await CliTestHelper.RunCliAsync(
            $"options create -o \"{outputDirectory}\"",
            TestContext.Current.CancellationToken);

        AssertNoCrash(createText);
        Assert.Equal(0, createExitCode);
        Assert.Contains("out]", createText, StringComparison.Ordinal);

        var (validateExitCode, validateText) = await CliTestHelper.RunCliAsync(
            $"options validate -o \"{outputDirectory}\"",
            TestContext.Current.CancellationToken);

        AssertNoCrash(validateText);
        Assert.Equal(0, validateExitCode);
        Assert.Contains("out]", validateText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrateValidate_WithBracketsInPaths_ReportsWithoutCrashing()
    {
        var slnPath = await WriteDummySolutionAsync();
        var (_, text) = await CliTestHelper.RunCliAsync(
            $"migrate validate -s \"{specPath}\" --solution \"{slnPath}\"",
            TestContext.Current.CancellationToken);

        AssertNoCrash(text);
        Assert.Contains("Migration Validator", text, StringComparison.Ordinal);
        Assert.Contains("work [2026]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrateExecute_WithBracketsInPaths_ReportsWithoutCrashing()
    {
        var slnPath = await WriteDummySolutionAsync();
        var (exitCode, text) = await CliTestHelper.RunCliAsync(
            $"migrate execute -s \"{specPath}\" --solution \"{slnPath}\" --dry-run --force",
            TestContext.Current.CancellationToken);

        AssertNoCrash(text);
        Assert.Equal(1, exitCode);
        Assert.Contains("cannot be migrated", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("work [2026]", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("spec validate -s \"{0}\"")]
    [InlineData("spec merge -s \"{0}\" --preview")]
    [InlineData("spec split -s \"{0}\" --preview")]
    [InlineData("generate server -s \"{0}\" -o \"{1}\" -n Demo")]
    [InlineData("generate client -s \"{0}\" -o \"{1}\" -n Demo.ApiClient")]
    [InlineData("generate client-typescript -s \"{0}\" -o \"{1}\"")]
    [InlineData("migrate validate -s \"{0}\" --solution \"{1}\"")]
    public async Task Command_WithMissingFileWhoseNameHasBracket_PrintsNotFoundAndReturnsOne(
        string argumentFormat)
    {
        var missingSpec = Path.Combine(specDirectory, "missing [v1].yaml");
        var arguments = string.Format(CultureInfo.InvariantCulture, argumentFormat, missingSpec, outputDirectory);

        var (exitCode, text) = await CliTestHelper.RunCliAsync(arguments, TestContext.Current.CancellationToken);

        AssertNoCrash(text);
        Assert.Equal(1, exitCode);
        Assert.Contains("not found", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing [v1].yaml", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OptionsValidate_WithMissingFileWhoseNameHasBracket_PrintsErrorAndReturnsOne()
    {
        var missingOptions = Path.Combine(outputDirectory, "missing [v1].json");

        var (exitCode, text) = await CliTestHelper.RunCliAsync(
            $"options validate -o \"{missingOptions}\"",
            TestContext.Current.CancellationToken);

        AssertNoCrash(text);
        Assert.Equal(1, exitCode);
        Assert.Contains("missing [v1].json", text, StringComparison.Ordinal);
    }

    private static void AssertNoCrash(string output)
    {
        foreach (var marker in CrashMarkers)
        {
            Assert.DoesNotContain(marker, output, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task<string> WriteDummySolutionAsync()
    {
        var slnDirectory = Path.Combine(outputDirectory, "sln [v1]");
        Directory.CreateDirectory(slnDirectory);
        var slnPath = Path.Combine(slnDirectory, "Contoso.sln");
        await File.WriteAllTextAsync(
            slnPath,
            "Microsoft Visual Studio Solution File, Format Version 12.00",
            TestContext.Current.CancellationToken);
        return slnPath;
    }
}