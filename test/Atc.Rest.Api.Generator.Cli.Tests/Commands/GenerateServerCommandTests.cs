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

    /// <summary>
    /// The command reads a specification as the build does: a parameter the server could not bind stops it (the build
    /// would fail), and so does an error in a part file - not only in the base file.
    /// </summary>
    [Theory]
    [InlineData("Shop.yaml", "  /pets:\n    get:\n      operationId: listPets\n      parameters:\n        - name: filter\n          in: query\n          schema: { type: object, properties: { name: { type: string } } }\n      responses:\n        '204':\n          description: OK\n")]
    [InlineData("Shop_Orders.yaml", "  /orders/{id}:\n    get:\n      operationId: getOrder\n      parameters:\n        - name: id\n          in: path\n          required: true\n          style: label\n          schema: { type: string }\n      responses:\n        '204':\n          description: OK\n")]
    public async Task GenerateServer_AParameterTheServerCannotBind_StopsTheCommand(
        string fileName,
        string paths)
    {
        // Arrange
        var specDir = Path.Combine(tempOutputDir, "specs");
        Directory.CreateDirectory(specDir);
        const string header = "openapi: 3.0.3\ninfo:\n  title: Shop\n  version: 1.0.0\npaths:\n";
        const string listPets = "  /pets:\n    get:\n      operationId: listPets\n      responses:\n        '204':\n          description: OK\n";
        var basePath = Path.Combine(specDir, "Shop.yaml");
        await File.WriteAllTextAsync(basePath, header + (fileName == "Shop.yaml" ? paths : listPets), TestContext.Current.CancellationToken);
        if (fileName != "Shop.yaml")
        {
            await File.WriteAllTextAsync(Path.Combine(specDir, fileName), header + paths, TestContext.Current.CancellationToken);
        }

        var repoRoot = Path.Combine(tempOutputDir, "Shop");

        // Act
        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            $"generate server -s \"{basePath}\" -o \"{repoRoot}\" -n \"Shop\"",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var cleanOutput = CliTestHelper.StripAnsiCodes(output);
        Assert.False(isSuccessful, cleanOutput);
        Assert.Contains(RuleIdentifiers.ParameterValueNotBindable, cleanOutput, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(repoRoot, "src")), cleanOutput);
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

    // dotnet run starts the host in Development with the API browser open, and outside Development the root does not
    // redirect to a page that is not mapped there
    [Theory]
    [InlineData("Scalar", "scalar/v1")]
    [InlineData("Swagger", "swagger")]
    public async Task GenerateServer_WritesLaunchSettings_AndRedirectsOnlyWhereTheUiIsMapped(
        string hostUi,
        string launchPath)
    {
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "LaunchSettings" + hostUi);
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n Contoso --host-ui {hostUi} --no-coding-rules";

        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {CliTestHelper.StripAnsiCodes(output)}");
        var hostFolder = Path.Combine(repoRoot, "src", "Contoso.Api");
        var launchSettings = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(hostFolder, "Properties", "launchSettings.json"), TestContext.Current.CancellationToken))!;
        foreach (var profile in new[] { "http", "https" })
        {
            Assert.Equal("Development", (string?)launchSettings["profiles"]![profile]!["environmentVariables"]!["ASPNETCORE_ENVIRONMENT"]);
            Assert.Equal(launchPath, (string?)launchSettings["profiles"]![profile]!["launchUrl"]);
            Assert.True((bool?)launchSettings["profiles"]![profile]!["launchBrowser"]);
        }

        Assert.StartsWith("http://localhost:", (string?)launchSettings["profiles"]!["http"]!["applicationUrl"], StringComparison.Ordinal);

        var program = (await File.ReadAllTextAsync(Path.Combine(hostFolder, "Program.cs"), TestContext.Current.CancellationToken)).Replace("\r\n", "\n", StringComparison.Ordinal);
        var developmentBlock = program[program.IndexOf("if (app.Environment.IsDevelopment())\n{", StringComparison.Ordinal)..];
        developmentBlock = developmentBlock[..developmentBlock.IndexOf("\n}", StringComparison.Ordinal)];
        Assert.Contains("Results.Redirect(\"/" + launchPath + "\")", developmentBlock, StringComparison.Ordinal);
        Assert.Equal(1, program.Split("Results.Redirect(").Length - 1);
    }

    [Fact]
    public async Task GenerateServer_TwoProjects_ProgramWiresTheGeneratedAddUseAndMap()
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
        Assert.Contains("builder.Services.AddContosoApi();", program, StringComparison.Ordinal);
        Assert.Contains("app.UseContosoApi();", program, StringComparison.Ordinal);
        Assert.Contains("app.MapContosoApi();", program, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two projects build: the host references the domain project, which holds the contracts and the handlers (both
    /// markers) and references nothing back. A host that held the contracts would reference the handlers' project
    /// that references it - a cycle restore stops on (MSB4006).
    /// </summary>
    [Fact]
    public async Task GenerateServer_TwoProjects_HostReferencesTheDomainProject_WhichHoldsTheContracts()
    {
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "TwoProjectsNoCycle");
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n Contoso --project-structure TwoProjects --no-coding-rules";

        var (isSuccessful, output) = await ProcessHelper.Execute(
            CliExeFile,
            arguments,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {CliTestHelper.StripAnsiCodes(output)}");
        var src = Path.Combine(repoRoot, "src");
        Assert.Equal(["Contoso.Api", "Contoso.Domain"], Directory.GetDirectories(src).Select(Path.GetFileName).Order(StringComparer.Ordinal), StringComparer.Ordinal);

        var host = await File.ReadAllTextAsync(Path.Combine(src, "Contoso.Api", "Contoso.Api.csproj"), TestContext.Current.CancellationToken);
        Assert.Contains("<ProjectReference Include=\"..\\Contoso.Domain\\Contoso.Domain.csproj\" />", host, StringComparison.Ordinal);
        Assert.Single(host.Split("<ProjectReference ").Skip(1));
        Assert.False(File.Exists(Path.Combine(src, "Contoso.Api", ".atc-rest-api-server")));

        var domain = await File.ReadAllTextAsync(Path.Combine(src, "Contoso.Domain", "Contoso.Domain.csproj"), TestContext.Current.CancellationToken);
        Assert.DoesNotContain("<ProjectReference", domain, StringComparison.Ordinal);
        Assert.Contains("<AdditionalFiles Include=\".atc-rest-api-server\" />", domain, StringComparison.Ordinal);
        Assert.Contains("<AdditionalFiles Include=\".atc-rest-api-server-handlers\" />", domain, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(src, "Contoso.Domain", ".atc-rest-api-server")));
        Assert.True(File.Exists(Path.Combine(src, "Contoso.Domain", ".atc-rest-api-server-handlers")));

        // The marker the domain step adds sits in the list as the others do, and the list still closes on its own line
        var domainLines = domain.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("\n    <AdditionalFiles Include=\".atc-rest-api-server-handlers\" />\n  </ItemGroup>\n", domainLines, StringComparison.Ordinal);

        // The host's Program.cs needs the implicit usings (WebApplication), also without the coding rules' props
        Assert.Contains("<ImplicitUsings>enable</ImplicitUsings>", host, StringComparison.Ordinal);
    }

    /// <summary>
    /// The host calls the methods the generator emits: named after the server's namespace (or apiName) and the domain
    /// project, not after the host project - <c>--namespace Acme.Shop</c> gives AddShopApi(), and a domain project
    /// <c>Contoso.Core</c> gives AddApiHandlersFromCore(), both in the Acme.Shop namespace.
    /// </summary>
    [Fact]
    public async Task GenerateServer_WithANamespaceAndADomainName_ProgramCallsTheGeneratedMethods()
    {
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "NamespacedHost");
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n Contoso --namespace Acme.Shop --domain-project Contoso.Core --no-coding-rules";

        var (isSuccessful, output) = await ProcessHelper.Execute(CliExeFile, arguments, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {CliTestHelper.StripAnsiCodes(output)}");
        var host = Path.Combine(repoRoot, "src", "Contoso.Api");
        var program = await File.ReadAllTextAsync(Path.Combine(host, "Program.cs"), TestContext.Current.CancellationToken);
        Assert.Contains("builder.Services.AddShopApi();", program, StringComparison.Ordinal);
        Assert.Contains("builder.Services.AddApiHandlersFromCore();", program, StringComparison.Ordinal);
        Assert.Contains("app.UseShopApi();", program, StringComparison.Ordinal);
        Assert.Contains("app.MapShopApi();", program, StringComparison.Ordinal);
        var globalUsings = await File.ReadAllTextAsync(Path.Combine(host, "GlobalUsings.cs"), TestContext.Current.CancellationToken);
        Assert.Contains("global using Acme.Shop;", globalUsings, StringComparison.Ordinal);
        Assert.Contains("global using Acme.Shop.Generated.Extensions;", globalUsings, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two projects with a domain project of another name and folder: the solution and the test project keep the base
    /// name, and every project reference and solution entry points at a project that is there.
    /// </summary>
    [Fact]
    public async Task GenerateServer_TwoProjects_DomainElsewhere_ReferencesTheProjectsWhereTheyAre()
    {
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "TwoProjectsDomainElsewhere");
        var domainFolder = Path.Combine(repoRoot, "lib", "Contoso.Core");
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n Contoso --project-structure TwoProjects --domain-project Contoso.Core --domain-output \"{domainFolder}\" --no-coding-rules";

        var (isSuccessful, output) = await ProcessHelper.Execute(CliExeFile, arguments, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {CliTestHelper.StripAnsiCodes(output)}");
        Assert.True(File.Exists(Path.Combine(repoRoot, "Contoso.slnx")), string.Join(", ", Directory.GetFiles(repoRoot)));
        Assert.True(File.Exists(Path.Combine(repoRoot, "test", "Contoso.Tests", "Contoso.Tests.csproj")));
        Assert.True(File.Exists(Path.Combine(domainFolder, "Contoso.Core.csproj")));

        foreach (var project in Directory.EnumerateFiles(repoRoot, "*.csproj", SearchOption.AllDirectories))
        {
            var content = await File.ReadAllTextAsync(project, TestContext.Current.CancellationToken);
            foreach (Match reference in Regex.Matches(content, "<ProjectReference Include=\"(?<path>[^\"]+)\"", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)))
            {
                var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, reference.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar)));
                Assert.True(File.Exists(target), $"{Path.GetFileName(project)} references {reference.Groups["path"].Value}, which is not there");
            }
        }

        var solution = await File.ReadAllTextAsync(Path.Combine(repoRoot, "Contoso.slnx"), TestContext.Current.CancellationToken);
        foreach (Match entry in Regex.Matches(solution, "<Project Path=\"(?<path>[^\"]+)\"", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)))
        {
            Assert.True(File.Exists(Path.Combine(repoRoot, entry.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar))), $"Contoso.slnx lists {entry.Groups["path"].Value}, which is not there");
        }
    }

    /// <summary>
    /// Two projects put both markers in the domain project: an existing domain project that lists only the handlers
    /// marker gets the server marker too (".atc-rest-api-server" is part of ".atc-rest-api-server-handlers", so it is
    /// matched as a whole Include).
    /// </summary>
    [Fact]
    public async Task GenerateServer_TwoProjects_ExistingDomainProject_GetsTheServerMarker()
    {
        var yamlPath = CliTestHelper.GetScenarioYamlPath("Demo");
        var repoRoot = Path.Combine(tempOutputDir, "TwoProjectsExistingDomain");
        var domainFolder = Path.Combine(repoRoot, "src", "Contoso.Domain");
        Directory.CreateDirectory(domainFolder);
        await File.WriteAllTextAsync(
            Path.Combine(domainFolder, "Contoso.Domain.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n\n  <ItemGroup>\n    <AdditionalFiles Include=\".atc-rest-api-server-handlers\" />\n  </ItemGroup>\n\n</Project>",
            TestContext.Current.CancellationToken);
        var arguments = $"generate server -s \"{yamlPath}\" -o \"{repoRoot}\" -n Contoso --project-structure TwoProjects --no-coding-rules";

        var (isSuccessful, output) = await ProcessHelper.Execute(CliExeFile, arguments, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(isSuccessful, $"Expected success but got failure. Output: {CliTestHelper.StripAnsiCodes(output)}");
        var domain = await File.ReadAllTextAsync(Path.Combine(domainFolder, "Contoso.Domain.csproj"), TestContext.Current.CancellationToken);
        Assert.Contains("<AdditionalFiles Include=\".atc-rest-api-server\" />", domain, StringComparison.Ordinal);
        Assert.Contains("<AdditionalFiles Include=\".atc-rest-api-server-handlers\" />", domain, StringComparison.Ordinal);
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