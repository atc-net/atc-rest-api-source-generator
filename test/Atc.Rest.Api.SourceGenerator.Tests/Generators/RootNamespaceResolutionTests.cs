namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// One rule gives every generator its root namespace: the marker's <c>namespace</c>, else an <c>info.title</c> that
/// already is a namespace, else the specification file's name. A server, its handlers and a client generated from the
/// same specification therefore agree without a marker saying so - and a marker <c>namespace</c> that is no namespace
/// is said to have been rewritten.
/// </summary>
public class RootNamespaceResolutionTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: {{title}}
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
                name: { type: string }
        """;

    public static TheoryData<string, string, string, string> Cases
        => new()
        {
            // The marker, info.title, the specification file, the root namespace
            { "{}", "Contoso.Billing", "api-1.yaml", "Contoso.Billing" },
            { "{}", "Billing", "api-1.yaml", "Billing" },
            { "{}", "The Billing API", "Billing.yaml", "Billing" },
            { "{}", "The Billing API", "billing-api.yaml", "BillingApi" },
            { """{ "namespace": "Fabrikam.Pay" }""", "Contoso.Billing", "api-1.yaml", "Fabrikam.Pay" },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Server_Handlers_AndClient_ResolveTheSameRootNamespace(
        string marker,
        string title,
        string specificationFile,
        string expected)
    {
        var yaml = Yaml.Replace("{{title}}", title, StringComparison.Ordinal);

        var server = Run(new ApiServerGenerator(), ".atc-rest-api-server", marker, specificationFile, yaml);
        var handlers = Run(new ApiServerDomainGenerator(), ".atc-rest-api-server-handlers", "{}", specificationFile, yaml);
        var client = Run(new ApiClientGenerator(), ".atc-rest-api-client", marker, specificationFile, yaml);

        Assert.Contains($"namespace {expected}.Generated.Models;", server.Sources, StringComparison.Ordinal);
        Assert.Contains($"namespace {expected}.Generated.Models;", client.Sources, StringComparison.Ordinal);

        // The handlers project has no marker namespace of its own here: its usings name the server's contracts
        if (marker == "{}")
        {
            Assert.Contains($"{expected}.Generated", handlers.Sources, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Contoso Api", "ContosoApi")]
    [InlineData("contoso-billing.v2", "ContosoBilling.v2")]
    public void AMarkerNamespaceThatIsNoNamespace_IsSaidToBeRewritten(
        string written,
        string used)
    {
        var yaml = Yaml.Replace("{{title}}", "The Billing API", StringComparison.Ordinal);
        var marker = $$"""{ "namespace": "{{written}}" }""";

        foreach (var (generator, markerFile) in new (IIncrementalGenerator, string)[] { (new ApiServerGenerator(), ".atc-rest-api-server"), (new ApiClientGenerator(), ".atc-rest-api-client") })
        {
            var run = Run(generator, markerFile, marker, "Billing.yaml", yaml);

            var info = Assert.Single(run.Diagnostics, d => d.Id == "ATC_API_GEN022");
            Assert.Equal(DiagnosticSeverity.Info, info.Severity);
            Assert.Contains($"'{written}'", info.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            Assert.Contains($"'{used}'", info.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            Assert.Contains($"namespace {used}.Generated.Models;", run.Sources, StringComparison.Ordinal);
        }

        // A namespace that is one is nothing to report
        Assert.DoesNotContain(
            Run(new ApiServerGenerator(), ".atc-rest-api-server", """{ "namespace": "Contoso.Api" }""", "Billing.yaml", yaml).Diagnostics,
            d => d.Id == "ATC_API_GEN022");
    }

    private static (string Sources, List<Diagnostic> Diagnostics) Run(
        IIncrementalGenerator generator,
        string markerFile,
        string marker,
        string specificationFile,
        string yaml)
    {
        var compilation = CSharpCompilation.Create(
            "Contoso.Host",
            [],
            CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: true),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(
            [
                new CompilationVerificationHarness.InMemoryAdditionalText(specificationFile, yaml),
                new CompilationVerificationHarness.InMemoryAdditionalText(markerFile, marker),
            ])
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, CancellationToken.None);

        var sources = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()));
        return (sources, [.. diagnostics]);
    }
}