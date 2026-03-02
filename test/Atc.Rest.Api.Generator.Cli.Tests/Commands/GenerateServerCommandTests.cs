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
}