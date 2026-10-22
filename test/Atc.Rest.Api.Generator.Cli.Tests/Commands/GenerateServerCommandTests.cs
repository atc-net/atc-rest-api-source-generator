namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// Tests for the generate server CLI command.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class GenerateServerCommandTests : IDisposable
{
    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    private readonly string tempOutputDir;

    public GenerateServerCommandTests()
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

    /// <summary>
    /// A <c>.yml</c> part beside a <c>.yaml</c> base goes into the repository and the contracts project with the base, as
    /// the build reads it - only parts with the base file's own extension were copied, so the build missed the part.
    /// </summary>
    [Fact]
    public async Task GenerateServer_APartWithAnotherExtension_IsCopiedAndListedWithTheBase()
    {
        // Arrange - the specification outside the repository, so the command copies it in
        var specDir = Path.Combine(tempOutputDir, "specs");
        Directory.CreateDirectory(specDir);
        var basePath = Path.Combine(specDir, "Shop.yaml");
        await File.WriteAllTextAsync(
            basePath,
            "openapi: 3.0.3\ninfo:\n  title: Shop\n  version: 1.0.0\npaths:\n  /pets:\n    get:\n      operationId: listPets\n      responses:\n        '204':\n          description: OK\n",
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(specDir, "Shop_Orders.yml"),
            "openapi: 3.0.3\ninfo:\n  title: Shop\n  version: 1.0.0\npaths:\n  /orders:\n    get:\n      operationId: listOrders\n      responses:\n        '204':\n          description: OK\n",
            TestContext.Current.CancellationToken);
        var repoRoot = Path.Combine(tempOutputDir, "Shop");
        var arguments = $"generate server -s \"{basePath}\" -o \"{repoRoot}\" -n \"Shop\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        Assert.NotEmpty(Directory.GetFiles(repoRoot, "Shop_Orders.yml", SearchOption.AllDirectories));
        var contractsProject = Assert.Single(Directory.GetFiles(repoRoot, "Shop.Api.Contracts.csproj", SearchOption.AllDirectories));
        Assert.Contains("Shop_Orders.yml", await File.ReadAllTextAsync(contractsProject, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateServer_WithProjectNameOnly_WritesNamespaceToMarkerFile()
    {
        // Arrange - Use a project name that differs from the YAML filename
        // to verify the namespace fallback derives from project name, not YAML filename.
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "PizzaPlanet");
        var projectName = "PizzaPlanet";
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n \"{projectName}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        // Default is ThreeProjects: {repoRoot}/src/{baseName}.Api.Contracts/.atc-rest-api-server
        var markerFilePath = Path.Combine(repoRoot, "src", "PizzaPlanet.Api.Contracts", ".atc-rest-api-server");
        Assert.True(File.Exists(markerFilePath), $"Marker file should exist at {markerFilePath}");

        var markerContent = await File.ReadAllTextAsync(markerFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("\"namespace\": \"PizzaPlanet\"", markerContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// The marker file is the user's after the first run: a second run with other options leaves a marker file
    /// the user edited as it is and says so, instead of rewriting it from the options.
    /// </summary>
    [Fact]
    public async Task GenerateServer_ASecondRun_KeepsAMarkerFileTheUserEdited()
    {
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "PizzaPlanet");
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n \"PizzaPlanet\"";
        var (firstOk, firstOutput) = await ProcessHelper.Execute(CliExeFile, arguments, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(firstOk, CliTestHelper.StripAnsiCodes(firstOutput));

        var markerFilePath = Path.Combine(repoRoot, "src", "PizzaPlanet.Api.Contracts", ".atc-rest-api-server");
        var edited = (await File.ReadAllTextAsync(markerFilePath, TestContext.Current.CancellationToken))
            .Replace("\"namespace\": \"PizzaPlanet\"", "\"namespace\": \"Contoso.Edited\"", StringComparison.Ordinal);
        await File.WriteAllTextAsync(markerFilePath, edited, TestContext.Current.CancellationToken);

        var (secondOk, secondOutput) = await ProcessHelper.Execute(CliExeFile, arguments, cancellationToken: TestContext.Current.CancellationToken);

        var cleanOutput = CliTestHelper.StripAnsiCodes(secondOutput);
        Assert.True(secondOk, cleanOutput);
        Assert.Contains("\"namespace\": \"Contoso.Edited\"", await File.ReadAllTextAsync(markerFilePath, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Contains("kept", cleanOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateServer_WithProjectNameWithSuffix_WritesStrippedNamespaceToMarkerFile()
    {
        // Arrange - Use a project name with a common suffix to verify ExtractSolutionName strips it
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "PizzaPlanetSuffix");
        var projectName = "PizzaPlanet.Api.Contracts";
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n \"{projectName}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        // baseName = ExtractSolutionName("PizzaPlanet.Api.Contracts") = "PizzaPlanet"
        // ThreeProjects default: contractsProjectName = "PizzaPlanet.Api.Contracts"
        var markerFilePath = Path.Combine(repoRoot, "src", "PizzaPlanet.Api.Contracts", ".atc-rest-api-server");
        Assert.True(File.Exists(markerFilePath), $"Marker file should exist at {markerFilePath}");

        var markerContent = await File.ReadAllTextAsync(markerFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("\"namespace\": \"PizzaPlanet\"", markerContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateServer_WithExplicitNamespace_WritesExplicitNamespaceToMarkerFile()
    {
        // Arrange - Explicit --namespace should take precedence over project name fallback
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "ExplicitNs");
        var projectName = "PizzaPlanet";
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n \"{projectName}\" --namespace MyCustomNamespace";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        var markerFilePath = Path.Combine(repoRoot, "src", "PizzaPlanet.Api.Contracts", ".atc-rest-api-server");
        Assert.True(File.Exists(markerFilePath), $"Marker file should exist at {markerFilePath}");

        var markerContent = await File.ReadAllTextAsync(markerFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("\"namespace\": \"MyCustomNamespace\"", markerContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateServer_WithContractsNamespace_WritesContractsNamespaceToMarkerFile()
    {
        // Arrange - --contracts-namespace should take highest precedence for server marker
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "ContractsNs");
        var projectName = "PizzaPlanet";
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n \"{projectName}\" --namespace Ignored --contracts-namespace ContractsNs";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        var markerFilePath = Path.Combine(repoRoot, "src", "PizzaPlanet.Api.Contracts", ".atc-rest-api-server");
        Assert.True(File.Exists(markerFilePath), $"Marker file should exist at {markerFilePath}");

        var markerContent = await File.ReadAllTextAsync(markerFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("\"namespace\": \"ContractsNs\"", markerContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateServer_TwoProjects_ProgramWiresProblemDetailsForEveryError()
    {
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "TwoProjectsProblemDetails");
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n Contoso --project-structure TwoProjects";

        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");
        var program = await File.ReadAllTextAsync(Path.Combine(repoRoot, "src", "Contoso.Api", "Program.cs"), TestContext.Current.CancellationToken);
        Assert.Contains("builder.Services.AddProblemDetails();", program, StringComparison.Ordinal);
        Assert.Contains("app.UseExceptionHandler();", program, StringComparison.Ordinal);
        Assert.Contains("app.UseStatusCodePages();", program, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateServer_WithDomainProject_WritesDomainNamespaceToHandlersMarkerFile()
    {
        // Arrange - Default ThreeProjects creates domain project with marker file
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "DomainNs");
        var projectName = "PizzaPlanet";
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n \"{projectName}\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.True(isSuccessful, $"Expected success but got failure. Output: {cleanOutput}");

        // ThreeProjects creates: {repoRoot}/src/PizzaPlanet.Domain/.atc-rest-api-server-handlers
        var domainMarkerFilePath = Path.Combine(repoRoot, "src", "PizzaPlanet.Domain", ".atc-rest-api-server-handlers");
        Assert.True(File.Exists(domainMarkerFilePath), $"Domain marker file should exist at {domainMarkerFilePath}");

        var domainMarkerContent = await File.ReadAllTextAsync(domainMarkerFilePath, TestContext.Current.CancellationToken);
        Assert.Contains("\"namespace\": \"PizzaPlanet\"", domainMarkerContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateServer_MarkerFiles_ReferenceTheirSchema_AndHaveOnlyKnownSettings()
    {
        // Arrange
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "SchemaRef");
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n \"PizzaPlanet\"";

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(isSuccessful, CliTestHelper.StripAnsiCodes(output));
        (string Path, string Schema, Type Config)[] markers =
        [
            (Path.Combine(repoRoot, "src", "PizzaPlanet.Api.Contracts", ".atc-rest-api-server"), Constants.JsonSchema.Server, typeof(ServerConfig)),
            (Path.Combine(repoRoot, "src", "PizzaPlanet.Domain", ".atc-rest-api-server-handlers"), Constants.JsonSchema.ServerHandlers, typeof(ServerDomainConfig)),
        ];
        foreach (var marker in markers)
        {
            var content = await File.ReadAllTextAsync(marker.Path, TestContext.Current.CancellationToken);
            using var document = JsonDocument.Parse(content);

            Assert.Equal("$schema", document.RootElement.EnumerateObject().First().Name);
            Assert.Equal(marker.Schema, document.RootElement.GetProperty("$schema").GetString());
            Assert.Empty(MarkerConfigInspector.FindProblems(content, marker.Config));
        }
    }
}