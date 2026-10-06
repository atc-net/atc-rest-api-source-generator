namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A specification with no operations (a models-only document, a multi-part base file whose parts are missing, an
/// overlay that removed <c>paths</c>) generates code that compiles - the unified <c>AddApi()</c>/<c>MapApi()</c>
/// extensions are not emitted, there being no endpoints or handlers for them to wire - and says so in a warning
/// (<c>ATC_API_PTH002</c>) from every generator.
/// </summary>
public sealed class EmptyDocumentGenerationTests
{
    private const string Specification = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths: {}
        components:
          schemas:
            Thing: { type: object, properties: { name: { type: string } } }
        """;

    private static readonly (IIncrementalGenerator Generator, string Marker)[] Generators =
    [
        (new ApiServerGenerator(), ".atc-rest-api-server"),
        (new ApiServerDomainGenerator(), ".atc-rest-api-server-handlers"),
        (new ApiClientGenerator(), ".atc-rest-api-client"),
    ];

    [Fact]
    public void NoOperations_EveryGeneratorReportsPTH002_AndWhatItEmitsCompiles()
    {
        var folder = Path.Combine(Path.GetTempPath(), "atc-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var serverSources = new List<(string HintName, string Source)>();
            foreach (var (generator, marker) in Generators)
            {
                // Every "Auto" MinimalApi mode on: the server generator emits its unified extensions when it can
                var compilation = CSharpCompilation.Create(
                    "Contoso.Empty",
                    references: CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: true),
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                var driver = CSharpGeneratorDriver
                    .Create(generator)
                    .AddAdditionalTexts([
                        new CompilationVerificationHarness.InMemoryAdditionalText(Path.Combine(folder, marker), "{}"),
                        new CompilationVerificationHarness.InMemoryAdditionalText(Path.Combine(folder, "Contoso.yaml"), Specification),
                    ]);
                driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, CancellationToken.None);
                var sources = driver.GetRunResult().GeneratedTrees.Select(t => (t.FilePath, t.GetText().ToString())).ToList();
                if (generator is ApiServerGenerator)
                {
                    serverSources = sources;
                }

                var warning = Assert.Single(diagnostics, d => d.Id == "ATC_API_PTH002");
                Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
                Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

                List<(string HintName, string Source)> toCompile = generator is ApiServerDomainGenerator ? [.. serverSources, .. sources] : sources;
                var errors = CompilationVerificationHarness.CompileGeneratedSources(toCompile, includeMinimalApi: true);
                Assert.True(errors.Count == 0, $"{marker}:\n" + string.Join("\n", errors));
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}