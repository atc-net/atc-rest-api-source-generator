namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// Tests for the generate client-typescript CLI command.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class GenerateClientTypeScriptCommandTests : IDisposable
{
    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    private readonly string tempOutputDir;

    public GenerateClientTypeScriptCommandTests()
    {
        tempOutputDir = Path.Combine(Path.GetTempPath(), "atc-rest-api-cli-ts-tests", Guid.NewGuid().ToString("N"));
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
    public async Task GenerateClientTypeScript_FirstTime_CreatesAllDirectoriesAndFiles()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTs");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("TypeScript client generation completed successfully", cleanOutput, StringComparison.Ordinal);

        // Verify output directories were created
        Assert.True(Directory.Exists(Path.Combine(outputPath, "models")), "models directory should exist");
        Assert.True(Directory.Exists(Path.Combine(outputPath, "errors")), "errors directory should exist");
        Assert.True(Directory.Exists(Path.Combine(outputPath, "types")), "types directory should exist");
        Assert.True(Directory.Exists(Path.Combine(outputPath, "client")), "client directory should exist");

        // Verify root barrel export
        Assert.True(File.Exists(Path.Combine(outputPath, "index.ts")), "Root index.ts should exist");
    }

    [Fact]
    public async Task GenerateClientTypeScript_WithShowcase_CreatesExpectedClientFiles()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Showcase");
        var outputPath = Path.Combine(tempOutputDir, "ShowcaseTs");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --no-strict";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        // Verify client directory has ApiClient + 6 segment clients
        var clientDir = Path.Combine(outputPath, "client");
        Assert.True(File.Exists(Path.Combine(clientDir, "ApiClient.ts")), "ApiClient.ts should exist");
        Assert.True(File.Exists(Path.Combine(clientDir, "AccountsClient.ts")), "AccountsClient.ts should exist");
        Assert.True(File.Exists(Path.Combine(clientDir, "TasksClient.ts")), "TasksClient.ts should exist");
        Assert.True(File.Exists(Path.Combine(clientDir, "UsersClient.ts")), "UsersClient.ts should exist");
        Assert.True(File.Exists(Path.Combine(clientDir, "FilesClient.ts")), "FilesClient.ts should exist");
        Assert.True(File.Exists(Path.Combine(clientDir, "NotificationsClient.ts")), "NotificationsClient.ts should exist");
        Assert.True(File.Exists(Path.Combine(clientDir, "TestingClient.ts")), "TestingClient.ts should exist");
        Assert.True(File.Exists(Path.Combine(clientDir, "index.ts")), "client/index.ts should exist");
    }

    [Fact]
    public async Task GenerateClientTypeScript_SecondTime_OverwritesFiles()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsIdempotent");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\"";

        // First run
        var (firstSuccess, _) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(firstSuccess, "First run should succeed");

        // Capture content from first run
        var rootIndexPath = Path.Combine(outputPath, "index.ts");
        var firstContent = await File.ReadAllTextAsync(rootIndexPath, TestContext.Current.CancellationToken);

        // Act - Run second time
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        // Files should still exist and have the same content (idempotent)
        var secondContent = await File.ReadAllTextAsync(rootIndexPath, TestContext.Current.CancellationToken);
        Assert.Equal(firstContent, secondContent);
    }

    [Fact]
    public async Task GenerateClientTypeScript_WithInvalidYaml_ReturnsError()
    {
        // Arrange - Create an invalid YAML file
        var invalidYamlPath = Path.Combine(tempOutputDir, "invalid.yaml");
        Directory.CreateDirectory(tempOutputDir);
        await File.WriteAllTextAsync(invalidYamlPath, "invalid: yaml: content:", TestContext.Current.CancellationToken);

        var outputPath = Path.Combine(tempOutputDir, "InvalidYamlOutput");
        var arguments = $"generate client-typescript -s \"{invalidYamlPath}\" -o \"{outputPath}\"";

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
    public async Task GenerateClientTypeScript_WithMissingSpecification_ReturnsError()
    {
        // Arrange
        var arguments = "generate client-typescript -o \"output\"";

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
    public async Task GenerateClientTypeScript_WithMissingOutput_ReturnsError()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"generate client-typescript -s \"{yamlPath}\"";

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
    public async Task GenerateClientTypeScript_WithNonExistentSpec_ReturnsError()
    {
        // Arrange
        var outputPath = Path.Combine(tempOutputDir, "NonExistentTest");
        var arguments = $"generate client-typescript -s \"nonexistent.yaml\" -o \"{outputPath}\"";

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
    public async Task GenerateClientTypeScript_Help_DisplaysUsage()
    {
        // Arrange
        var arguments = "generate client-typescript --help";

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
        Assert.Contains("--no-strict", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--enum-style", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClientTypeScript_DisplaysGenerationCounts()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsCounts");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("Models generated:", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Enums generated:", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Error types generated:", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Types generated:", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("Clients generated:", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClientTypeScript_Models_ContainExpectedTypeDeclarations()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Showcase");
        var outputPath = Path.Combine(tempOutputDir, "ShowcaseTsModels");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --no-strict";

        // Act
        var (isSuccessful, _) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful);

        // Assert — check a model file contains export interface with properties
        var modelsDir = Path.Combine(outputPath, "models");
        var modelFiles = Directory.GetFiles(modelsDir, "*.ts").Where(f => !f.EndsWith("index.ts", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.True(modelFiles.Length > 0, "Should have generated model files");

        // Check Account model specifically
        var accountFile = Path.Combine(modelsDir, "Account.ts");
        Assert.True(File.Exists(accountFile), "Account.ts should exist");
        var accountContent = await File.ReadAllTextAsync(accountFile, TestContext.Current.CancellationToken);
        Assert.Contains("export interface", accountContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClientTypeScript_Enums_ContainExpectedValues()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Showcase");
        var outputPath = Path.Combine(tempOutputDir, "ShowcaseTsEnums");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --no-strict";

        // Act
        var (isSuccessful, _) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful);

        // Assert — check that enums directory has files
        var enumsDir = Path.Combine(outputPath, "enums");
        Assert.True(Directory.Exists(enumsDir), "enums directory should exist");

        var enumFiles = Directory.GetFiles(enumsDir, "*.ts").Where(f => !f.EndsWith("index.ts", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.True(enumFiles.Length >= 5, $"Should have at least 5 enum files, found {enumFiles.Length}");

        // Check UserRole enum specifically
        var userRoleFile = Path.Combine(enumsDir, "UserRole.ts");
        Assert.True(File.Exists(userRoleFile), "UserRole.ts should exist");
        var userRoleContent = await File.ReadAllTextAsync(userRoleFile, TestContext.Current.CancellationToken);

        // Default style is Union type
        Assert.Contains("export type", userRoleContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClientTypeScript_ErrorTypes_ContainApiErrorAndValidationError()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsErrors");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\"";

        // Act
        var (isSuccessful, _) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful);

        // Assert
        var errorsDir = Path.Combine(outputPath, "errors");
        Assert.True(Directory.Exists(errorsDir), "errors directory should exist");

        var apiErrorContent = await File.ReadAllTextAsync(
            Path.Combine(errorsDir, "ApiError.ts"), TestContext.Current.CancellationToken);
        Assert.Contains("extends Error", apiErrorContent, StringComparison.Ordinal);

        var validationErrorContent = await File.ReadAllTextAsync(
            Path.Combine(errorsDir, "ValidationError.ts"), TestContext.Current.CancellationToken);
        Assert.Contains("extends ApiError", validationErrorContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClientTypeScript_ApiResult_ContainsDiscriminatedUnion()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsResult");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\"";

        // Act
        var (isSuccessful, _) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful);

        // Assert
        var apiResultContent = await File.ReadAllTextAsync(
            Path.Combine(outputPath, "types", "ApiResult.ts"), TestContext.Current.CancellationToken);

        // Should have the discriminated union with status arms
        Assert.Contains("status: 'ok'", apiResultContent, StringComparison.Ordinal);
        Assert.Contains("status: 'badRequest'", apiResultContent, StringComparison.Ordinal);
        Assert.Contains("status: 'notFound'", apiResultContent, StringComparison.Ordinal);
        Assert.Contains("status: 'serverError'", apiResultContent, StringComparison.Ordinal);

        // Should have type guard functions
        Assert.Contains("export function isOk", apiResultContent, StringComparison.Ordinal);
        Assert.Contains("export function isBadRequest", apiResultContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClientTypeScript_ClientMethods_HaveCorrectSignatures()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Showcase");
        var outputPath = Path.Combine(tempOutputDir, "ShowcaseTsClient");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --no-strict";

        // Act
        var (isSuccessful, _) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful);

        // Assert — spot-check AccountsClient methods
        var accountsClientContent = await File.ReadAllTextAsync(
            Path.Combine(outputPath, "client", "AccountsClient.ts"), TestContext.Current.CancellationToken);

        // Per-op result types replaced the generic ApiResult return type, but the method
        // bodies still funnel through ApiClient. Spot-check the discriminator names that
        // the per-op aliases must include — those are the contract for consumers.
        Assert.Contains("async", accountsClientContent, StringComparison.Ordinal);
        Assert.Contains("ApiClient", accountsClientContent, StringComparison.Ordinal);
        Assert.Contains("export type ", accountsClientContent, StringComparison.Ordinal);
        Assert.Contains("status: 'ok'", accountsClientContent, StringComparison.Ordinal);
        Assert.Contains("status: 'parseError'", accountsClientContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClientTypeScript_BarrelExports_ReExportAllModules()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Showcase");
        var outputPath = Path.Combine(tempOutputDir, "ShowcaseTsBarrel");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --no-strict";

        // Act
        var (isSuccessful, _) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful);

        // Assert — root index.ts should re-export all subdirectories
        var rootIndexContent = await File.ReadAllTextAsync(
            Path.Combine(outputPath, "index.ts"), TestContext.Current.CancellationToken);

        Assert.Contains("./models", rootIndexContent, StringComparison.Ordinal);
        Assert.Contains("./enums", rootIndexContent, StringComparison.Ordinal);
        Assert.Contains("./errors", rootIndexContent, StringComparison.Ordinal);
        Assert.Contains("./types", rootIndexContent, StringComparison.Ordinal);
        Assert.Contains("./client", rootIndexContent, StringComparison.Ordinal);

        // Subdirectory barrel exports should exist and re-export their types
        var modelsIndexContent = await File.ReadAllTextAsync(
            Path.Combine(outputPath, "models", "index.ts"), TestContext.Current.CancellationToken);
        Assert.Contains("export", modelsIndexContent, StringComparison.Ordinal);

        var clientIndexContent = await File.ReadAllTextAsync(
            Path.Combine(outputPath, "client", "index.ts"), TestContext.Current.CancellationToken);
        Assert.Contains("ApiClient", clientIndexContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// Type-checks the hook files, which the Showcase test below never generates, under
    /// <c>noUnusedLocals</c>.
    /// </summary>
    /// <remarks>
    /// The existing strict test passes no <c>--hooks</c> flag, so no hook file has ever been
    /// compiled by anything; and its tsconfig omits <c>noUnusedLocals</c>. Between them, a
    /// generated SWR hook file carrying an unused <c>ApiResult</c> import went unnoticed — code
    /// that fails to build for any consumer using that compiler option, or the equivalent ESLint
    /// rule. Both hook styles are covered because they are separate emitters with separate import
    /// logic.
    /// </remarks>
    [Theory]
    [Trait("Category", "RequiresNode")]
    [InlineData("Swr")]
    [InlineData("ReactQuery")]
    public async Task GenerateClientTypeScript_Hooks_CompileWithNoUnusedLocals(
        string hooksStyle)
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            Assert.Skip(skipReason);
        }

        var yamlPath = CliTestHelper.GetScenarioYamlPath("Showcase");
        var outputPath = Path.Combine(tempOutputDir, $"Hooks{hooksStyle}Tsc");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --hooks {hooksStyle} --no-strict";

        var (isSuccessful, genOutput) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful, $"Generation should succeed. Output: {CliTestHelper.StripAnsiCodes(genOutput)}");

        // React's types are not installed here, so the hook files are checked for their own
        // correctness rather than against the React type definitions.
        var tsconfigContent = """
            {
              "compilerOptions": {
                "target": "ES2022",
                "module": "ES2022",
                "moduleResolution": "bundler",
                "strict": true,
                "noEmit": true,
                "noUnusedLocals": true,
                "noUnusedParameters": true,
                "skipLibCheck": true,
                "lib": ["ES2022", "DOM", "DOM.Iterable"]
              },
              "include": ["./**/*.ts"]
            }
            """;
        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "tsconfig.json"),
            tsconfigContent,
            TestContext.Current.CancellationToken);

        // The shared, pinned workspace gives the output directory its TypeScript
        NodeWorkspace.Link(outputPath);

        var tscOutput = NodeToolchain.RunNpx("tsc --noEmit", outputPath, TimeSpan.FromMinutes(5)).Output;

        // Missing React/SWR/TanStack packages produce TS2307 "cannot find module", which is an
        // artefact of not installing them rather than a defect in the generated code. Unused
        // declarations (TS6133/TS6196) are exactly what this test exists to catch.
        var unusedDiagnostics = tscOutput
            .Split('\n')
            .Where(l => l.Contains("TS6133", StringComparison.Ordinal) || l.Contains("TS6196", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            unusedDiagnostics.Count == 0,
            $"Generated {hooksStyle} output declares or imports things it never uses:\n" +
            string.Join("\n", unusedDiagnostics));
    }

    [Fact]
    [Trait("Category", "RequiresNode")]
    public async Task GenerateClientTypeScript_Showcase_CompilesWithTscStrict()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            Assert.Skip(skipReason);
        }

        // Arrange — generate TypeScript from Showcase.yaml
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Showcase");
        var outputPath = Path.Combine(tempOutputDir, "ShowcaseTsc");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --no-strict";

        var (isSuccessful, genOutput) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        var cleanGenOutput = CliTestHelper.StripAnsiCodes(genOutput);
        Assert.True(isSuccessful, $"Generation should succeed. Output: {cleanGenOutput}");

        // Write tsconfig.json for strict compilation
        var tsconfigContent = """
            {
              "compilerOptions": {
                "target": "ES2022",
                "module": "ES2022",
                "moduleResolution": "bundler",
                "strict": true,
                "noEmit": true,
                "skipLibCheck": false,
                "lib": ["ES2022", "DOM", "DOM.Iterable"]
              },
              "include": ["./**/*.ts"]
            }
            """;
        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "tsconfig.json"),
            tsconfigContent,
            TestContext.Current.CancellationToken);

        // The shared, pinned workspace gives the output directory its TypeScript
        NodeWorkspace.Link(outputPath);

        // Act — run tsc via npx
        var tsc = NodeToolchain.RunNpx("tsc --noEmit", outputPath, TimeSpan.FromMinutes(5));

        // Assert
        Assert.True(tsc.ExitCode == 0, $"tsc --strict should compile without errors. tsc output:\n{tsc.Output}");
    }

    [Fact]
    public async Task DryRun_ReportsCountsWithoutWritingFiles()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsDryRun");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --dry-run";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("(dry run)", cleanOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Models generated:", cleanOutput, StringComparison.Ordinal);
        Assert.False(Directory.Exists(outputPath), "Output directory should NOT exist in dry-run mode");
    }

    [Fact]
    public async Task DryRun_ShowsDryRunLabel()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsDryLabel");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --dry-run";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("(dry run)", cleanOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Watch_AppearsInHelp()
    {
        // Arrange
        var arguments = "generate client-typescript --help";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful);
        Assert.Contains("--watch", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("--dry-run", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AxiosClient_GeneratesAxiosApiClient()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsAxios");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --client-type Axios";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        var apiClientContent = await File.ReadAllTextAsync(
            Path.Combine(outputPath, "client", "ApiClient.ts"), TestContext.Current.CancellationToken);
        Assert.Contains("axios", apiClientContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReactQueryHooks_GeneratesHookFiles()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsHooks");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --hooks ReactQuery";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        var hooksDir = Path.Combine(outputPath, "hooks");
        Assert.True(Directory.Exists(hooksDir), "hooks directory should exist");

        var hookFiles = Directory.GetFiles(hooksDir, "use*.ts");
        Assert.True(hookFiles.Length > 0, "Should have generated hook files matching use*.ts");
    }

    [Fact]
    public async Task PizzaPlanet_ReactQueryHooks_FileUploadWithPathParams()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("PizzaPlanet");
        var outputPath = Path.Combine(tempOutputDir, "PizzaPlanetHooks");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --hooks ReactQuery --no-strict";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        // Bug 1: Hook file for pizzas should have pizzaId in the upload mutation
        var hooksDir = Path.Combine(outputPath, "hooks");
        var pizzaHookFile = Path.Combine(hooksDir, "usePizzas.ts");
        Assert.True(File.Exists(pizzaHookFile), "usePizzas.ts hook file should exist");
        var hookContent = await File.ReadAllTextAsync(pizzaHookFile, TestContext.Current.CancellationToken);
        Assert.Contains("pizzaId", hookContent, StringComparison.Ordinal);

        // Bug 3: Client files should use explicit field + constructor, not parameter property shorthand
        var pizzasClientFile = Path.Combine(outputPath, "client", "PizzasClient.ts");
        Assert.True(File.Exists(pizzasClientFile), "PizzasClient.ts should exist");
        var clientContent = await File.ReadAllTextAsync(pizzasClientFile, TestContext.Current.CancellationToken);
        Assert.Contains("private readonly api: ApiClient;", clientContent, StringComparison.Ordinal);
        Assert.DoesNotContain("constructor(private readonly", clientContent, StringComparison.Ordinal);

        // Bug 2: ApiService.ts should use type keyword on ApiClientOptions import
        var apiServiceFile = Path.Combine(outputPath, "client", "ApiService.ts");
        Assert.True(File.Exists(apiServiceFile), "ApiService.ts should exist");
        var apiServiceContent = await File.ReadAllTextAsync(apiServiceFile, TestContext.Current.CancellationToken);
        Assert.Contains("type ApiClientOptions", apiServiceContent, StringComparison.Ordinal);

        // Bug 4: Client files should not import PizzaImageUploadRequest (unused ref type for file uploads)
        Assert.DoesNotContain("PizzaImageUploadRequest", clientContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZodSchemas_GeneratesZodFiles()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsZod");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --zod";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        var modelsDir = Path.Combine(outputPath, "models");
        var zodFiles = Directory.GetFiles(modelsDir, "*.zod.ts");
        Assert.True(zodFiles.Length > 0, "Should have generated .zod.ts files in models directory");
    }

    [Fact]
    public async Task Scaffold_GeneratesPackageJson()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsScaffold");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --scaffold";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        Assert.True(File.Exists(Path.Combine(outputPath, "package.json")), "package.json should exist");
        Assert.True(File.Exists(Path.Combine(outputPath, "tsconfig.json")), "tsconfig.json should exist");
    }

    /// <summary>
    /// The scaffold files (package.json, the tsconfigs, README.md) are the user's after the first run: a second run
    /// leaves an edited one as it is.
    /// </summary>
    [Fact]
    public async Task Scaffold_ASecondRun_KeepsTheScaffoldFilesTheUserEdited()
    {
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsScaffoldTwice");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --scaffold";
        var (firstOk, firstOutput) = await ProcessHelper.Execute(CliExeFile, arguments, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(firstOk, CliTestHelper.StripAnsiCodes(firstOutput));

        var packageJsonPath = Path.Combine(outputPath, "package.json");
        var edited = (await File.ReadAllTextAsync(packageJsonPath, TestContext.Current.CancellationToken))
            .Replace("\"private\": true", "\"private\": true,\n  \"description\": \"edited by hand\"", StringComparison.Ordinal);
        Assert.Contains("edited by hand", edited, StringComparison.Ordinal);
        await File.WriteAllTextAsync(packageJsonPath, edited, TestContext.Current.CancellationToken);

        var (secondOk, secondOutput) = await ProcessHelper.Execute(CliExeFile, arguments, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(secondOk, CliTestHelper.StripAnsiCodes(secondOutput));
        Assert.Contains("edited by hand", await File.ReadAllTextAsync(packageJsonPath, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>
    /// A file the generator would write that exists without the generated header is somebody's own file: it is kept,
    /// and the run says so.
    /// </summary>
    [Fact]
    public async Task AFileWithoutTheGeneratedHeader_IsKept()
    {
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsOwnFile");
        var ownFile = Path.Combine(outputPath, "client", "ApiClient.ts");
        Directory.CreateDirectory(Path.GetDirectoryName(ownFile)!);
        await File.WriteAllTextAsync(ownFile, "// mine\nexport class ApiClient {}\n", TestContext.Current.CancellationToken);
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\"";

        var (isSuccessful, output) = await ProcessHelper.Execute(CliExeFile, arguments, cancellationToken: TestContext.Current.CancellationToken);

        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, cleanOutput);
        Assert.Equal("// mine\nexport class ApiClient {}\n", await File.ReadAllTextAsync(ownFile, TestContext.Current.CancellationToken));
        Assert.Contains("ApiClient.ts", cleanOutput, StringComparison.Ordinal);
        Assert.Contains("kept", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertDates_ConvertsTheDeclaredDatesThroughTheWireShapes()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsDates");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --convert-dates";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        // Only the declared dates are converted (helpers/wireNames.ts), not every ISO-looking string (no global reviver)
        var apiClientContent = await File.ReadAllTextAsync(
            Path.Combine(outputPath, "client", "ApiClient.ts"), TestContext.Current.CancellationToken);
        Assert.DoesNotContain("dateReviver", apiClientContent, StringComparison.Ordinal);
        var wireNames = await File.ReadAllTextAsync(
            Path.Combine(outputPath, "helpers", "wireNames.ts"), TestContext.Current.CancellationToken);
        Assert.Contains("{ date: 'date-time' }", wireNames, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoReadonly_OmitsReadonlyModifier()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsMutable");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --no-readonly";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        var modelsDir = Path.Combine(outputPath, "models");
        var modelFiles = Directory.GetFiles(modelsDir, "*.ts")
            .Where(f => !f.EndsWith("index.ts", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(modelFiles.Length > 0, "Should have generated model files");

        foreach (var modelFile in modelFiles)
        {
            var content = await File.ReadAllTextAsync(modelFile, TestContext.Current.CancellationToken);
            Assert.DoesNotContain("readonly ", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task NamingStrategyOriginal_PreservesPropertyNames()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsOriginal");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --naming-strategy Original";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.Contains("TypeScript client generation completed successfully", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnumStyleEnum_GeneratesTsEnums()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, "DemoTsEnumStyle");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\" --enum-style Enum";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        var enumsDir = Path.Combine(outputPath, "enums");
        Assert.True(Directory.Exists(enumsDir), "enums directory should exist");

        var enumFiles = Directory.GetFiles(enumsDir, "*.ts")
            .Where(f => !f.EndsWith("index.ts", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(enumFiles.Length > 0, "Should have generated enum files");

        // At least one enum file should use export enum syntax
        var anyHasExportEnum = false;
        foreach (var enumFile in enumFiles)
        {
            var content = await File.ReadAllTextAsync(enumFile, TestContext.Current.CancellationToken);
            if (content.Contains("export enum", StringComparison.Ordinal))
            {
                anyHasExportEnum = true;
                break;
            }
        }

        Assert.True(anyHasExportEnum, "At least one enum file should contain 'export enum'");
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(" --no-extensible-enums", false)]
    public async Task UnionEnums_AreOpenByDefault_AndClosedWithNoExtensibleEnums(
        string option,
        bool open)
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var outputPath = Path.Combine(tempOutputDir, open ? "DemoTsOpenEnums" : "DemoTsClosedEnums");
        var arguments = $"generate client-typescript -s \"{yamlPath}\" -o \"{outputPath}\"{option}";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        var enumFiles = Directory.GetFiles(Path.Combine(outputPath, "enums"), "*.ts")
            .Where(f => !f.EndsWith("index.ts", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(enumFiles);

        foreach (var enumFile in enumFiles)
        {
            var content = await File.ReadAllTextAsync(enumFile, TestContext.Current.CancellationToken);
            Assert.Equal(open, content.Contains("| (string & {});", StringComparison.Ordinal));
        }
    }
}