namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A parameter whose value the generated code cannot bind or send (<c>ATC_API_OPR037</c>; here a nested object in
/// the query, as Spring's <c>pageable</c>): a client generator warns and leaves the parameter out - the rest of the
/// operation, and every other operation, is generated and compiles; a server generator reports an error, as its
/// code could not bind it.
/// </summary>
public sealed class UnbindableParameterTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /items:
            get:
              operationId: listItems
              parameters:
                - name: pageable
                  in: query
                  required: true
                  schema: { $ref: '#/components/schemas/Pageable' }
                - name: limit
                  in: query
                  schema: { type: integer }
              responses:
                '204':
                  description: Listed
        components:
          schemas:
            Pageable:
              type: object
              properties:
                page: { type: integer }
                sort: { $ref: '#/components/schemas/Sort' }
            Sort:
              type: object
              properties:
                orderBy: { type: array, items: { type: string } }
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AClient_LeavesTheParameterOut_WithAWarning(bool typedClient)
    {
        var marker = typedClient
            ? """{ "generationMode": "TypedClient" }"""
            : ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails");

        var (diagnostics, sources) = Run(new ApiClientGenerator(), ".atc-rest-api-client", marker, includeMinimalApi: false);

        var warning = Assert.Single(diagnostics, d => d.Id == RuleIdentifiers.ParameterValueNotBindable);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("'pageable'", warning.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);

        var assembly = CompilationVerificationHarness.EmitAndLoad(sources);
        var parameters = assembly.GetTypes().Single(t => t.Name == "ListItemsParameters").GetConstructors().Single().GetParameters();
        Assert.Equal(["limit"], parameters.Select(p => p.Name!.ToLowerInvariant()), StringComparer.Ordinal);
    }

    [Fact]
    public void AServer_ReportsAnError()
    {
        var (diagnostics, _) = Run(new ApiServerGenerator(), ".atc-rest-api-server", ErrorResponseBodyRuntimeTests.ServerMarker, includeMinimalApi: true);

        var error = Assert.Single(diagnostics, d => d.Id == RuleIdentifiers.ParameterValueNotBindable);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("'pageable'", error.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static (List<Diagnostic> Diagnostics, List<(string HintName, string Source)> Sources) Run(
        IIncrementalGenerator generator,
        string markerFileName,
        string markerContent,
        bool includeMinimalApi)
    {
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new CompilationVerificationHarness.InMemoryAdditionalText("ErrorBodies.yaml", Yaml),
            new CompilationVerificationHarness.InMemoryAdditionalText(markerFileName, markerContent));
        var compilation = CSharpCompilation.Create(
            "ErrorBodies",
            references: CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = (CSharpGeneratorDriver)CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, TestContext.Current.CancellationToken);

        return ([.. diagnostics], [.. driver.GetRunResult().GeneratedTrees.Select(t => (t.FilePath, t.GetText().ToString()))]);
    }
}