namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The domain generator writes the handler scaffolds, their signatures and GlobalUsings.cs into the project - on a save
/// in the IDE as on a build. These tests pin how it does so safely: never over a file another host wrote, without
/// temporary files left behind, not at all for a specification with errors or where AtcRestApiScaffoldHandlers is
/// false (a CI build: the missing handler is reported, the stub is still built), and with the contracts namespace
/// from a sibling project's marker that is a declared AdditionalFiles input.
/// </summary>
public sealed class HandlerScaffoldWriteTests
{
    private const string ProjectName = "Contoso.Api.Domain";

    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: Contoso
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Thing'
        components:
          schemas:
            Thing:
              type: object
              required: [name]
              properties:
                name:
                  type: string
        """;

    [Fact]
    public void Scaffold_ByDefault_IsWrittenOnce_AndNeverOverwritten()
    {
        using var project = new TemporaryProject();
        var handlerPath = Path.Combine(project.Directory, "ApiHandlers", "ListThingsHandler.cs");

        var (diagnostics, _) = Run(project, Yaml);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.True(File.Exists(handlerPath));
        Assert.True(File.Exists(Path.Combine(project.Directory, "GlobalUsings.cs")));

        // A file another host wrote meanwhile is left as it is
        File.WriteAllText(handlerPath, "// written by another host");
        Run(project, Yaml);
        Assert.Equal("// written by another host", File.ReadAllText(handlerPath));

        // Written through a temporary file next to it, which is gone
        Assert.Empty(Directory.GetFiles(project.Directory, "*.tmp", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData(" false ")]
    public void Scaffold_WhenScaffoldHandlersIsFalse_IsNotWritten_ButBuiltAndReported(
        string value)
    {
        using var project = new TemporaryProject();

        var (diagnostics, generated) = Run(project, Yaml, scaffoldHandlers: value);

        // Nothing in the project: no scaffold, no GlobalUsings.cs
        Assert.Empty(Directory.GetFiles(project.Directory, "*.cs", SearchOption.AllDirectories));

        // The stub is part of the build, and the missing handler is said
        Assert.Contains(generated, source => source.Contains("class ListThingsHandler", StringComparison.Ordinal));
        var warning = Assert.Single(diagnostics, d => d.Id == "ATC_API_GEN024");
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("ListThingsHandler", warning.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    [InlineData("")]
    public void Scaffold_WhenScaffoldHandlersIsNotFalse_IsWritten(string? value)
    {
        using var project = new TemporaryProject();

        var (diagnostics, _) = Run(project, Yaml, scaffoldHandlers: value);

        Assert.True(File.Exists(Path.Combine(project.Directory, "ApiHandlers", "ListThingsHandler.cs")));
        Assert.DoesNotContain(diagnostics, d => d.Id == "ATC_API_GEN024");
    }

    [Fact]
    public void Scaffold_ForASpecificationWithErrors_WritesNothing()
    {
        using var project = new TemporaryProject();

        // The operation is half typed: its items refer to a schema that is not there (yet)
        var halfTyped = Yaml.Replace("#/components/schemas/Thing'", "#/components/schemas/Thin'", StringComparison.Ordinal);

        var (diagnostics, _) = Run(project, halfTyped);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(Directory.GetFiles(project.Directory, "*.cs", SearchOption.AllDirectories));
    }

    [Fact]
    public void ContractsNamespace_IsReadFromADeclaredSiblingMarker()
    {
        using var project = new TemporaryProject();

        // A sibling project's marker the package's targets add: declared, not on disk
        var siblingMarker = Path.Combine(Path.GetDirectoryName(project.Directory)!, "Contoso.Api.Contracts", ".atc-rest-api-server");

        var (_, generated) = Run(
            project,
            Yaml,
            scaffoldHandlers: "false",
            siblingMarker: (siblingMarker, """{ "namespace": "Fabrikam.Billing" }"""));

        var globalUsings = Assert.Single(generated, source => source.Contains("global using ", StringComparison.Ordinal) && !source.Contains("class ", StringComparison.Ordinal));
        Assert.Contains("Fabrikam.Billing", globalUsings, StringComparison.Ordinal);
    }

    [Fact]
    public void ServerGenerator_IgnoresADeclaredSiblingMarker()
    {
        var siblingMarker = Path.Combine(Path.GetTempPath(), "Contoso.Api.Contracts", ".atc-rest-api-server");
        var texts = new List<AdditionalText>
        {
            new CompilationVerificationHarness.InMemoryAdditionalText(ProjectName + ".yaml", Yaml),
            new CompilationVerificationHarness.InMemoryAdditionalText(siblingMarker, "{}"),
        };

        var options = new Options(scaffoldHandlers: null, siblingMarkerPath: siblingMarker);
        var driver = CSharpGeneratorDriver
            .Create([new ApiServerGenerator().AsSourceGenerator()], optionsProvider: options)
            .AddAdditionalTexts([.. texts]);

        var result = driver.RunGenerators(Compilation(), TestContext.Current.CancellationToken).GetRunResult();

        // The sibling's marker is that project's: this one generates no server
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Writer_CreatesAFileOnce_WhenSeveralHostsWriteItAtOnce()
    {
        using var project = new TemporaryProject();
        var filePath = Path.Combine(project.Directory, "Handlers", "ListThingsHandler.cs");

        var created = new ConcurrentBag<bool>();
        Parallel.For(0, 16, i => created.Add(ScaffoldFileWriter.TryCreate(filePath, $"// host {i}\n")));

        Assert.Single(created, c => c);
        Assert.Matches("^// host [0-9]+\n?$", File.ReadAllText(filePath).Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Empty(Directory.GetFiles(project.Directory, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Writer_ReplacesAFile_InTheEncodingGiven()
    {
        using var project = new TemporaryProject();
        var filePath = Path.Combine(project.Directory, "Handler.cs");
        File.WriteAllText(filePath, "before");

        ScaffoldFileWriter.Replace(filePath, "after", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var bytes = File.ReadAllBytes(filePath);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        Assert.Equal("after", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        Assert.Empty(Directory.GetFiles(project.Directory, "*.tmp"));
    }

    [Fact]
    public void Writer_ReplacesAFile_ThatAnotherProcessHasOpen()
    {
        using var project = new TemporaryProject();
        var filePath = Path.Combine(project.Directory, "GlobalUsings.cs");
        File.WriteAllText(filePath, "before");

        // An editor or the compiler reading it: Windows cannot swap a file in under an open one
        using (new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            ScaffoldFileWriter.Write(filePath, "global using Contoso;");
        }

        Assert.Equal("global using Contoso;", File.ReadAllText(filePath));
        Assert.Empty(Directory.GetFiles(project.Directory, "*.tmp"));
    }

    private static (List<Diagnostic> Diagnostics, List<string> Generated) Run(
        TemporaryProject project,
        string yaml,
        string? scaffoldHandlers = null,
        (string Path, string Content)? siblingMarker = null)
    {
        var texts = new List<AdditionalText>
        {
            new CompilationVerificationHarness.InMemoryAdditionalText(Path.Combine(project.Directory, "Contoso.yaml"), yaml),
            new CompilationVerificationHarness.InMemoryAdditionalText(Path.Combine(project.Directory, ".atc-rest-api-server-handlers"), "{}"),
        };

        if (siblingMarker is { } sibling)
        {
            texts.Add(new CompilationVerificationHarness.InMemoryAdditionalText(sibling.Path, sibling.Content));
        }

        var driver = CSharpGeneratorDriver
            .Create([new ApiServerDomainGenerator().AsSourceGenerator()], optionsProvider: new Options(scaffoldHandlers, siblingMarker?.Path))
            .AddAdditionalTexts([.. texts]);

        driver.RunGeneratorsAndUpdateCompilation(Compilation(), out var output, out var diagnostics, TestContext.Current.CancellationToken);
        return ([.. diagnostics], [.. output.SyntaxTrees.Select(t => t.ToString())]);
    }

    private static CSharpCompilation Compilation()
        => CSharpCompilation.Create(
            ProjectName,
            [],
            CompilationVerificationHarness.GetFullFrameworkReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    // The build's options: AtcRestApiScaffoldHandlers, and the AtcRestApiSiblingMarker metadata of one additional file
    private sealed class Options(
        string? scaffoldHandlers,
        string? siblingMarkerPath) : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
    {
        private readonly Values global = new(scaffoldHandlers is null ? [] : new Dictionary<string, string>(StringComparer.Ordinal) { ["build_property.AtcRestApiScaffoldHandlers"] = scaffoldHandlers });
        private readonly Values sibling = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["build_metadata.AdditionalFiles.AtcRestApiSiblingMarker"] = "true" });
        private readonly Values none = new([]);

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions
            => global;

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(
            SyntaxTree tree)
            => none;

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(
            AdditionalText textFile)
            => string.Equals(textFile.Path, siblingMarkerPath, StringComparison.Ordinal) ? sibling : none;

        private sealed class Values(Dictionary<string, string> values) : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
        {
            public override bool TryGetValue(
                string key,
                [NotNullWhen(true)] out string? value)
                => values.TryGetValue(key, out value);
        }
    }

    // A project folder of its own, removed afterwards
    private sealed class TemporaryProject : IDisposable
    {
        public TemporaryProject()
        {
            Directory = Path.Combine(Path.GetTempPath(), "atc-scaffold-" + Guid.NewGuid().ToString("N"), ProjectName);
            System.IO.Directory.CreateDirectory(Directory);
        }

        public string Directory { get; }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Path.GetDirectoryName(Directory)!, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup
            }
        }
    }
}