namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// The generate commands take their settings from ApiGeneratorOptions.json: every value in the file is used
/// (a CLI flag still wins), and a file that cannot be read stops the command instead of being replaced by defaults.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class GenerateCommandOptionsFileTests : IDisposable
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
    [InlineData("server")]
    [InlineData("client")]
    public async Task Generate_OptionsFileWithUnknownEnumValue_FailsNamingTheFile(
        string target)
    {
        var optionsPath = WriteOptionsFile(target, """{ "server": { "subFolderStrategy": "BySegment" } }""");

        var (isSuccessful, output) = await Run(target, "Contoso", $"--options \"{optionsPath}\"");

        Assert.False(isSuccessful, $"Expected failure but got success. Output: {output}");
        Assert.Contains(ApiGeneratorOptions.FileName, output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(tempOutputDir, target, "out")), "Nothing should be generated");
    }

    [Theory]
    [InlineData("server")]
    [InlineData("client")]
    public async Task Generate_ExplicitOptionsPathMissing_Fails(string target)
    {
        var (isSuccessful, output) = await Run(target, "Contoso", $"--options \"{Path.Combine(tempOutputDir, "missing.json")}\"");

        Assert.False(isSuccessful, $"Expected failure but got success. Output: {output}");
        Assert.Contains("missing.json", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateServer_ServerNamespaceInOptionsFile_IsWrittenToTheMarker()
    {
        var optionsPath = WriteOptionsFile("server", """{ "server": { "namespace": "Contoso.Pets" } }""");

        var (isSuccessful, output) = await Run("server", "Contoso", $"--options \"{optionsPath}\"");

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {output}");
        var marker = await ReadFile("server", "src", "Contoso.Api.Contracts", ".atc-rest-api-server");
        Assert.Contains("\"namespace\": \"Contoso.Pets\"", marker, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateServer_NamespaceFlag_WinsOverTheOptionsFile()
    {
        var optionsPath = WriteOptionsFile("server", """{ "server": { "namespace": "Contoso.Pets" } }""");

        var (isSuccessful, output) = await Run("server", "Contoso", $"--options \"{optionsPath}\" --namespace Contoso.Flag");

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {output}");
        var marker = await ReadFile("server", "src", "Contoso.Api.Contracts", ".atc-rest-api-server");
        Assert.Contains("\"namespace\": \"Contoso.Flag\"", marker, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateServer_ScaffoldingValuesInOptionsFile_AreUsed()
    {
        var optionsPath = WriteOptionsFile(
            "server",
            """
            {
              "scaffolding": {
                "hostProjectName": "Contoso.Host",
                "contractsProjectName": "Contoso.Contracts",
                "domainProjectName": "Contoso.Logic",
                "hostNamespace": "Contoso.HostNs",
                "contractsNamespace": "Contoso.ContractsNs",
                "domainNamespace": "Contoso.LogicNs",
                "aspire": true
              }
            }
            """);

        var (isSuccessful, output) = await Run("server", "Contoso", $"--options \"{optionsPath}\"");

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {output}");
        var contractsMarker = await ReadFile("server", "src", "Contoso.Contracts", ".atc-rest-api-server");
        Assert.Contains("\"namespace\": \"Contoso.ContractsNs\"", contractsMarker, StringComparison.Ordinal);
        var domainMarker = await ReadFile("server", "src", "Contoso.Logic", ".atc-rest-api-server-handlers");
        Assert.Contains("\"namespace\": \"Contoso.LogicNs\"", domainMarker, StringComparison.Ordinal);
        var hostProject = await ReadFile("server", "src", "Contoso.Host", "Contoso.Host.csproj");
        Assert.Contains("<RootNamespace>Contoso.HostNs</RootNamespace>", hostProject, StringComparison.Ordinal);
        Assert.True(Directory.Exists(OutputPath("server", "src", "Contoso.Aspire")), $"The Aspire project should be created. Output: {output}");
    }

    [Fact]
    public async Task GenerateServer_HostNamespaceFlag_IsTheHostRootNamespace()
    {
        var (isSuccessful, output) = await Run("server", "Contoso", "--host-namespace Contoso.Web");

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {output}");
        var hostProject = await ReadFile("server", "src", "Contoso.Api", "Contoso.Api.csproj");
        Assert.Contains("<RootNamespace>Contoso.Web</RootNamespace>", hostProject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClient_ClientNamespaceInOptionsFile_IsWrittenToTheMarker()
    {
        var optionsPath = WriteOptionsFile("client", """{ "client": { "namespace": "Contoso.Pets.Client" } }""");

        var (isSuccessful, output) = await Run("client", "Contoso.ApiClient", $"--options \"{optionsPath}\"");

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {output}");
        var marker = await ReadFile("client", ".atc-rest-api-client");
        Assert.Contains("\"namespace\": \"Contoso.Pets.Client\"", marker, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateClient_ScaffoldingClientValuesInOptionsFile_AreUsed()
    {
        var optionsPath = WriteOptionsFile(
            "client",
            """{ "scaffolding": { "clientProjectName": "Contoso.Gateway", "clientNamespace": "Contoso.GatewayNs" } }""");

        var (isSuccessful, output) = await Run("client", "Contoso.ApiClient", $"--options \"{optionsPath}\"");

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {output}");
        Assert.True(File.Exists(OutputPath("client", "Contoso.Gateway.csproj")), $"The project should be named from the options file. Output: {output}");
        var marker = await ReadFile("client", ".atc-rest-api-client");
        Assert.Contains("\"namespace\": \"Contoso.GatewayNs\"", marker, StringComparison.Ordinal);
    }

    private string WriteOptionsFile(
        string target,
        string json)
    {
        var folder = Path.Combine(tempOutputDir, target, "options");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, ApiGeneratorOptions.FileName);
        File.WriteAllText(path, json);
        return path;
    }

    private string OutputPath(
        string target,
        params string[] parts)
        => Path.Combine([tempOutputDir, target, "out", .. parts]);

    private Task<string> ReadFile(
        string target,
        params string[] parts)
        => File.ReadAllTextAsync(OutputPath(target, parts), TestContext.Current.CancellationToken);

    private async Task<(bool IsSuccessful, string Output)> Run(
        string target,
        string projectName,
        string extraArguments)
    {
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var arguments = $"generate {target} -s \"{yamlPath}\" -o \"{OutputPath(target)}\" -n \"{projectName}\" {extraArguments}";

        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        return (isSuccessful, CliTestHelper.StripAnsiCodes(output));
    }
}