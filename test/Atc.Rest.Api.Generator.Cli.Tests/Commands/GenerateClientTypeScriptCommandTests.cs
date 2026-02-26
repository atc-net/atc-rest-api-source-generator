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

        // Should have async methods returning ApiResult
        Assert.Contains("async", accountsClientContent, StringComparison.Ordinal);
        Assert.Contains("ApiResult", accountsClientContent, StringComparison.Ordinal);
        Assert.Contains("ApiClient", accountsClientContent, StringComparison.Ordinal);
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

    [Fact]
    [Trait("Category", "RequiresNode")]
    public async Task GenerateClientTypeScript_Showcase_CompilesWithTscStrict()
    {
        // Skip if Node.js is not available
        var nodeAvailable = await IsNodeAvailable(TestContext.Current.CancellationToken);
        if (!nodeAvailable)
        {
            return;
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

        // Install TypeScript locally in the output directory
        var (npmSuccess, npmOutput) = await RunCommand(
            "npm",
            "install --save-dev typescript",
            outputPath,
            TestContext.Current.CancellationToken);

        Assert.True(npmSuccess, $"npm install typescript should succeed. Output:\n{npmOutput}");

        // Act — run tsc via npx
        var (tscSuccess, tscOutput) = await RunCommand(
            "npx",
            "tsc --noEmit",
            outputPath,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(tscSuccess, $"tsc --strict should compile without errors. tsc output:\n{tscOutput}");
    }

    private static async Task<bool> IsNodeAvailable(
        CancellationToken cancellationToken)
    {
        try
        {
            var (success, _) = await RunCommand("node", "--version", Directory.GetCurrentDirectory(), cancellationToken);
            return success;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(bool IsSuccessful, string Output)> RunCommand(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        // On Windows, .cmd scripts (node, npx) require cmd.exe to execute
        var isWindows = OperatingSystem.IsWindows();
        var actualFileName = isWindows ? "cmd.exe" : fileName;
        var actualArguments = isWindows ? $"/c {fileName} {arguments}" : arguments;

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = actualFileName,
            Arguments = actualArguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        var outputBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode == 0, outputBuilder.ToString());
    }
}