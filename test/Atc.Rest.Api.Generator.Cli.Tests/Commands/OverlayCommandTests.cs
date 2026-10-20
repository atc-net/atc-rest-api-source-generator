namespace Atc.Rest.Api.Generator.Cli.Tests.Commands;

/// <summary>
/// OpenAPI Overlays on the command line: <c>spec overlay apply</c> writes the specification an overlay makes,
/// <c>--overlay</c> applies one before <c>spec validate</c> and <c>generate client-typescript</c>, and
/// <c>generate client</c> / <c>generate server</c> copy the overlay into the project and list it in the marker, so
/// the build applies it.
/// </summary>
[Collection("Sequential-CLI")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort cleanup in tests.")]
public sealed class OverlayCommandTests : IDisposable
{
    private const string Spec = """
        openapi: 3.0.4
        info:
          title: Metering
          version: 1.0.0
        paths:
          /api/meteringpoints/{id}:
            get:
              parameters:
                - name: id
                  in: path
                  required: true
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/MeteringPoint'
        components:
          schemas:
            MeteringPoint:
              title: MeteringPoint
              type: object
              required: [id]
              properties:
                id:
                  type: string
        """;

    private const string Overlay = """
        overlay: 1.2.0
        info:
          title: Our fixes
          version: 1.0.0
        actions:
          - target: "$.paths['/api/meteringpoints/{id}'].get"
            update:
              operationId: getMeteringPoint
              tags: [MeteringPoints]
        """;

    private static readonly FileInfo CliExeFile = CliTestHelper.GetCliExecutableFile();

    private readonly string tempDir;
    private readonly string specPath;
    private readonly string overlayPath;

    public OverlayCommandTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "atc-rest-api-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        specPath = Path.Combine(tempDir, "Metering.yaml");
        overlayPath = Path.Combine(tempDir, "Metering.overlay.yaml");
        File.WriteAllText(specPath, Spec);
        File.WriteAllText(overlayPath, Overlay);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDir))
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public async Task SpecOverlayApply_WritesTheSpecificationTheOverlayMakes()
    {
        var output = Path.Combine(tempDir, "effective.yaml");

        var (isSuccessful, log) = await Run($"spec overlay apply -s \"{specPath}\" --overlay \"{overlayPath}\" -o \"{output}\"");

        Assert.True(isSuccessful, log);
        var effective = await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken);
        Assert.Contains("operationId: getMeteringPoint", effective, StringComparison.Ordinal);
        Assert.Contains("openapi: 3.0.4", effective, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecOverlayApply_WithFileNamesRelativeToTheCurrentDirectory()
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = CliExeFile.FullName,
            Arguments = "spec overlay apply -s Metering.yaml --overlay Metering.overlay.yaml -o effective.yaml",
            WorkingDirectory = tempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = System.Diagnostics.Process.Start(startInfo)!;
        var log = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.True(process.ExitCode == 0, CliTestHelper.StripAnsiCodes(log));
        Assert.Contains("operationId: getMeteringPoint", await File.ReadAllTextAsync(Path.Combine(tempDir, "effective.yaml"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecOverlayApply_TargetThatSelectsNothing_IsAWarning()
    {
        var stale = Path.Combine(tempDir, "stale.overlay.yaml");
        await File.WriteAllTextAsync(stale, Overlay.Replace("/api/meteringpoints/{id}", "/api/renamed", StringComparison.Ordinal), TestContext.Current.CancellationToken);

        var (isSuccessful, log) = await Run($"spec overlay apply -s \"{specPath}\" --overlay \"{stale}\" -o \"{Path.Combine(tempDir, "out.yaml")}\"");

        Assert.True(isSuccessful, log);
        Assert.Contains("selects nothing", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecOverlayApply_InvalidOverlay_Fails_NamingTheAction()
    {
        var invalid = Path.Combine(tempDir, "invalid.overlay.yaml");
        await File.WriteAllTextAsync(invalid, "overlay: 1.2.0\ninfo: { title: t, version: 1 }\nactions:\n  - target: \"$.paths[\"\n    remove: true\n", TestContext.Current.CancellationToken);

        var (isSuccessful, log) = await Run($"spec overlay apply -s \"{specPath}\" --overlay \"{invalid}\" -o \"{Path.Combine(tempDir, "out.yaml")}\"");

        Assert.False(isSuccessful, log);
        Assert.Contains("actions[0]", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecValidate_WithTheOverlay_PassesStrict_AndWithoutItFails()
    {
        var (withoutOverlay, _) = await Run($"spec validate -s \"{specPath}\"");
        var (withOverlay, log) = await Run($"spec validate -s \"{specPath}\" --overlay \"{overlayPath}\"");

        Assert.False(withoutOverlay);
        Assert.True(withOverlay, log);
    }

    [Fact]
    public async Task GenerateClientTypeScript_AppliesTheOverlay()
    {
        var output = Path.Combine(tempDir, "ts");

        var (isSuccessful, log) = await Run($"generate client-typescript -s \"{specPath}\" -o \"{output}\" --overlay \"{overlayPath}\"");

        Assert.True(isSuccessful, log);
        var client = Directory.GetFiles(Path.Combine(output, "client"), "*.ts").Select(File.ReadAllText).First(c => c.Contains("getMeteringPoint", StringComparison.Ordinal));
        Assert.Contains("async getMeteringPoint(", client, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>generate client-typescript</c> reads the options file too: its <c>general.overlays</c> are applied before the
    /// <c>--overlay</c> ones.
    /// </summary>
    [Fact]
    public async Task GenerateClientTypeScript_AppliesTheOverlaysOfTheOptionsFile_BeforeTheCommandLines()
    {
        var output = Path.Combine(tempDir, "ts-options");
        var second = Path.Combine(tempDir, "Second.overlay.yaml");
        await File.WriteAllTextAsync(
            second,
            """
            overlay: 1.2.0
            info:
              title: A summary
              version: 1.0.0
            actions:
              - target: "$.paths['/api/meteringpoints/{id}'].get"
                update:
                  summary: Reads a metering point
            """,
            TestContext.Current.CancellationToken);
        var optionsPath = Path.Combine(tempDir, "ApiGeneratorOptions.json");
        await File.WriteAllTextAsync(optionsPath, """{ "general": { "overlays": [ "Metering.overlay.yaml" ] } }""", TestContext.Current.CancellationToken);

        // The specification has no operationId without the options file's overlay: Strict validation would stop the command
        var (isSuccessful, log) = await Run($"generate client-typescript -s \"{specPath}\" -o \"{output}\" --options \"{optionsPath}\" --overlay \"{second}\"");

        Assert.True(isSuccessful, log);
        var client = Directory.GetFiles(Path.Combine(output, "client"), "*.ts").Select(File.ReadAllText).First(c => c.Contains("getMeteringPoint", StringComparison.Ordinal));
        Assert.Contains("async getMeteringPoint(", client, StringComparison.Ordinal);
        Assert.Contains("Reads a metering point", client, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("client", ".atc-rest-api-client")]
    [InlineData("server", ".atc-rest-api-server")]
    public async Task Generate_CopiesTheOverlayIntoTheProject_AndListsItInTheMarker(
        string command,
        string markerFileName)
    {
        var output = Path.Combine(tempDir, command);

        var (isSuccessful, log) = await Run($"generate {command} -s \"{specPath}\" -o \"{output}\" -n Metering --overlay \"{overlayPath}\"");

        Assert.True(isSuccessful, log);
        var marker = Directory.GetFiles(output, markerFileName, SearchOption.AllDirectories)[0];
        var markerDirectory = Path.GetDirectoryName(marker)!;
        var markerContent = await File.ReadAllTextAsync(marker, TestContext.Current.CancellationToken);
        Assert.Contains("\"overlays\"", markerContent, StringComparison.Ordinal);
        Assert.Contains("Metering.overlay.yaml", markerContent, StringComparison.Ordinal);

        var csproj = await File.ReadAllTextAsync(Directory.GetFiles(markerDirectory, "*.csproj").Single(), TestContext.Current.CancellationToken);
        Assert.Contains("Metering.overlay.yaml", csproj, StringComparison.Ordinal);
        var listed = (string)System.Text.Json.Nodes.JsonNode.Parse(markerContent)!["overlays"]![0]!;
        Assert.True(File.Exists(Path.GetFullPath(Path.Combine(markerDirectory, listed))), $"{listed} is not next to the marker in {markerDirectory}");
    }

    /// <summary>
    /// The same from the options file: its <c>general.overlays</c> are applied - before the <c>--overlay</c> ones - so
    /// a team writes its fixes down once instead of on every command line.
    /// </summary>
    [Theory]
    [InlineData("client", ".atc-rest-api-client")]
    [InlineData("server", ".atc-rest-api-server")]
    public async Task Generate_AppliesTheOverlaysOfTheOptionsFile_BeforeTheCommandLines(
        string command,
        string markerFileName)
    {
        var output = Path.Combine(tempDir, command + "-options");
        var second = Path.Combine(tempDir, "Second.overlay.yaml");
        await File.WriteAllTextAsync(
            second,
            """
            overlay: 1.2.0
            info:
              title: A summary
              version: 1.0.0
            actions:
              - target: "$.paths['/api/meteringpoints/{id}'].get"
                update:
                  summary: Reads a metering point
            """,
            TestContext.Current.CancellationToken);
        var optionsPath = Path.Combine(tempDir, "ApiGeneratorOptions.json");
        await File.WriteAllTextAsync(optionsPath, """{ "general": { "overlays": [ "Metering.overlay.yaml" ] } }""", TestContext.Current.CancellationToken);

        // The specification has no operationId without the options file's overlay: Strict validation would stop the command
        var (isSuccessful, log) = await Run($"generate {command} -s \"{specPath}\" -o \"{output}\" -n Metering --options \"{optionsPath}\" --overlay \"{second}\"");

        Assert.True(isSuccessful, log);
        var marker = Directory.GetFiles(output, markerFileName, SearchOption.AllDirectories)[0];
        var overlays = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(marker, TestContext.Current.CancellationToken))!["overlays"]!
            .AsArray()
            .Select(o => Path.GetFileName((string)o!))
            .ToList();
        Assert.Equal(["Metering.overlay.yaml", "Second.overlay.yaml"], overlays);
    }

    private static async Task<(bool IsSuccessful, string Output)> Run(
        string arguments)
    {
        var (isSuccessful, output) = await ProcessHelper.Execute(CliExeFile, arguments, cancellationToken: TestContext.Current.CancellationToken);
        return (isSuccessful, CliTestHelper.StripAnsiCodes(output));
    }
}