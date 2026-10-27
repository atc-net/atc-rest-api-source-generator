namespace Atc.Rest.Api.SourceGenerator.Tests.Validators;

/// <summary>
/// Response <c>links</c> are the one OpenAPI feature the generators drop without a trace: both report
/// <c>ATC_API_OPR035</c> for them. <c>readOnly</c>/<c>writeOnly</c>, callbacks and several <c>servers</c> are
/// supported, so nothing is reported for them - the old <c>ATCAPI_FEAT001-004</c> informations are gone.
/// </summary>
public sealed class UnsupportedFeatureDiagnosticsTests
{
    private const string Links = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: OK
                  links:
                    next:
                      operationId: listThings
        """;

    private const string ReadOnly = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
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
                        $ref: '#/components/schemas/Thing'
        components:
          schemas:
            Thing:
              type: object
              properties:
                id:
                  type: string
                  readOnly: true
                secret:
                  type: string
                  writeOnly: true
        """;

    private const string Callbacks = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        paths:
          /things:
            post:
              operationId: createThing
              responses:
                '201':
                  description: Created
              callbacks:
                onEvent:
                  '{$request.body#/url}':
                    post:
                      responses:
                        '200':
                          description: OK
        """;

    private const string Servers = """
        openapi: 3.0.3
        info: { title: Contoso, version: 1.0.0 }
        servers:
          - url: https://api.contoso.com
          - url: https://staging.contoso.com
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: OK
        """;

    [Theory]
    [InlineData(".atc-rest-api-server")]
    [InlineData(".atc-rest-api-client")]
    public void ResponseLinks_AreReportedAsOPR035Information(
        string markerFileName)
    {
        var diagnostics = Run(markerFileName, Links);

        var info = Assert.Single(diagnostics, d => string.Equals(d.Id, Generator.RuleIdentifiers.ResponseLinksDropped, StringComparison.Ordinal));
        Assert.Equal(DiagnosticSeverity.Info, info.Severity);
        Assert.Contains("listThings", info.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("ATCAPI_", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(".atc-rest-api-server", ReadOnly)]
    [InlineData(".atc-rest-api-server", Callbacks)]
    [InlineData(".atc-rest-api-server", Servers)]
    [InlineData(".atc-rest-api-client", ReadOnly)]
    [InlineData(".atc-rest-api-client", Callbacks)]
    [InlineData(".atc-rest-api-client", Servers)]
    public void SupportedFeatures_ReportNothing(
        string markerFileName,
        string specification)
    {
        var diagnostics = Run(markerFileName, specification);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("ATCAPI_", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, Generator.RuleIdentifiers.ResponseLinksDropped, StringComparison.Ordinal));
    }

    private static ImmutableArray<Diagnostic> Run(
        string markerFileName,
        string specification)
    {
        IIncrementalGenerator generator = markerFileName switch
        {
            ".atc-rest-api-client" => new ApiClientGenerator(),
            _ => new ApiServerGenerator(),
        };

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("Api.yaml", specification),
            new InMemoryAdditionalText(markerFileName, "{}"));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Http.HttpContext).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var generatorDiagnostics, CancellationToken.None);

        return generatorDiagnostics;
    }
}