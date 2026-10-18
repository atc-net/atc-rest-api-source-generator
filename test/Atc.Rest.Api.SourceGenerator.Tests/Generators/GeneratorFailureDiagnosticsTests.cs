namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// The catch around each generator step turns an exception into the step's diagnostic, naming the file and the
/// exception (a step that fails before the specification is known names the marker), and the generator keeps running (no CS8785). No known input makes a step throw (the fuzz tests assert
/// it), so the step is made to fail through <see cref="GeneratorFaultInjection"/>.
/// </summary>
public class GeneratorFailureDiagnosticsTests
{
    private const string Specification = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: OK
        """;

    [Fact]
    public void AServerGenerationFailure_IsReportedAsGEN001()
        => AssertTheFailureIsReported(new ApiServerGenerator(), ".atc-rest-api-server", RuleIdentifiers.ServerGenerationError, ".atc-rest-api-server");

    [Fact]
    public void AClientGenerationFailure_IsReportedAsGEN003()
        => AssertTheFailureIsReported(new ApiClientGenerator(), ".atc-rest-api-client", RuleIdentifiers.ClientGenerationError, ".atc-rest-api-client");

    [Fact]
    public void AHandlerScaffoldGenerationFailure_IsReportedAsGEN005()
        => AssertTheFailureIsReported(new ApiServerDomainGenerator(), ".atc-rest-api-server-handlers", RuleIdentifiers.HandlerScaffoldGenerationError, ".atc-rest-api-server-handlers");

    [Fact]
    public void AnEndpointInjectionFailure_IsReportedAsGEN008()
    {
        const string source = """
            namespace Contoso.Host;

            [Atc.Rest.Api.SourceGenerator.EndpointRegistration("Contoso")]
            public partial class Endpoints
            {
            }
            """;
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            CompilationVerificationHarness.GetMinimalReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(new ApiEndpointInjectionGenerator())
            .AddAdditionalTexts([new InMemoryAdditionalText("Contoso.yaml", Specification)]);

        ImmutableArray<Diagnostic> diagnostics;
        using (GeneratorFaultInjection.FailAt(RuleIdentifiers.EndpointInjectionGenerationError))
        {
            driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out diagnostics, TestContext.Current.CancellationToken);
        }

        AssertReported(diagnostics, RuleIdentifiers.EndpointInjectionGenerationError, "Endpoints");
    }

    private static void AssertTheFailureIsReported(
        IIncrementalGenerator generator,
        string markerFileName,
        string ruleId,
        string expectedFileInMessage)
    {
        var directory = Path.Combine(Path.GetTempPath(), "atc-generator-failure-tests", Guid.NewGuid().ToString("N"));
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references: CompilationVerificationHarness.GetFullFrameworkReferences(false),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(generator).AddAdditionalTexts(
        [
            new InMemoryAdditionalText(Path.Combine(directory, "Contoso.yaml"), Specification),
            new InMemoryAdditionalText(Path.Combine(directory, markerFileName), "{}"),
        ]);

        ImmutableArray<Diagnostic> diagnostics;
        using (GeneratorFaultInjection.FailAt(ruleId))
        {
            driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out diagnostics, TestContext.Current.CancellationToken);
        }

        AssertReported(diagnostics, ruleId, expectedFileInMessage);
        Assert.False(Directory.Exists(directory), "The failing step wrote files.");
    }

    private static void AssertReported(
        ImmutableArray<Diagnostic> diagnostics,
        string ruleId,
        string expectedInMessage)
    {
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8785");
        var diagnostic = Assert.Single(diagnostics, d => d.Id == ruleId);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains(expectedInMessage, message, StringComparison.Ordinal);
        Assert.Contains($"A test made the step reported as {ruleId} fail.", message, StringComparison.Ordinal);
    }
}