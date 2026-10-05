namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// An OpenAPI Overlay listed in the marker (<c>"overlays": [...]</c>) is applied to the specification before it is
/// validated and generated - by the server, the handler and the client generators alike. An overlay file is never
/// taken for the specification or one of its parts, and what goes wrong is reported: a missing or invalid overlay is
/// an error, a target that selects nothing a warning, an overlay file nobody lists a note.
/// </summary>
public sealed class OverlayGenerationTests
{
    // A third-party style specification: no operationIds, so Strict validation fails (ATC_API_OPR001) and the names
    // would come from the paths
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
          /api/internal/debug:
            get:
              responses:
                '204':
                  description: No Content
        components:
          schemas:
            MeteringPoint:
              type: object
              properties:
                id:
                  type: string
        """;

    private const string Overlay = """
        overlay: 1.2.0
        info:
          title: Our fixes to the metering API
          version: 1.0.0
        actions:
          - target: $.paths['/api/meteringpoints/{id}'].get
            update:
              operationId: getMeteringPoint
          - target: $.paths['/api/internal/debug']
            remove: true
        """;

    private const string ClientMarker = """{ "generationMode": "EndpointPerOperation", "validateSpecificationStrategy": "Strict", "overlays": ["Metering.overlay.yaml"] }""";

    [Fact]
    public void Client_GeneratesFromTheSpecificationAsTheOverlayChangesIt()
    {
        var (sources, diagnostics) = Run(new ApiClientGenerator(), ".atc-rest-api-client", ClientMarker, ("Metering.overlay.yaml", Overlay));

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(sources, s => s.HintName.Contains("GetMeteringPointEndpoint", StringComparison.Ordinal));
        Assert.DoesNotContain(sources, s => s.Source.Contains("internal/debug", StringComparison.Ordinal));
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources));
    }

    [Fact]
    public void WithoutTheOverlay_StrictValidationFails()
    {
        var (_, diagnostics) = Run(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation", "validateSpecificationStrategy": "Strict" }""");

        Assert.Contains(diagnostics, d => d.Id == "ATC_API_OPR001");
    }

    [Fact]
    public void Server_AndHandlers_ApplyTheOverlayToo()
    {
        var serverMarker = """{ "validateSpecificationStrategy": "Strict", "overlays": ["Metering.overlay.yaml"] }""";
        var (server, serverDiagnostics) = Run(new ApiServerGenerator(), ".atc-rest-api-server", serverMarker, ("Metering.overlay.yaml", Overlay));
        var (handlers, handlerDiagnostics) = Run(new ApiServerDomainGenerator(), ".atc-rest-api-server-handlers", serverMarker, ("Metering.overlay.yaml", Overlay));

        Assert.DoesNotContain(serverDiagnostics.Concat(handlerDiagnostics), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(server, s => s.Source.Contains("IGetMeteringPointHandler", StringComparison.Ordinal));
        Assert.DoesNotContain(server, s => s.Source.Contains("internal/debug", StringComparison.Ordinal));
        Assert.Contains(handlers, s => s.Source.Contains("GetMeteringPointHandler", StringComparison.Ordinal));
    }

    [Fact]
    public void OverlayFile_IsNotTakenForTheSpecification_EvenWhenItsNameWouldBeTheBase()
    {
        // "Metering.overlay" has no underscore: by name alone it could be the base file
        var (sources, diagnostics) = Run(new ApiClientGenerator(), ".atc-rest-api-client", ClientMarker, ("Metering.overlay.yaml", Overlay));

        Assert.DoesNotContain(diagnostics, d => d.Id is "ATC_API_GEN004" or "ATC_API_GEN003");
        Assert.NotEmpty(sources);
    }

    [Fact]
    public void ListedOverlayThatIsMissing_IsAnError_AndNothingIsGenerated_ReportsGEN015()
    {
        var (sources, diagnostics) = Run(new ApiClientGenerator(), ".atc-rest-api-client", ClientMarker);

        var error = Assert.Single(diagnostics, d => d.Id == "ATC_API_GEN015");
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("Metering.overlay.yaml", error.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.DoesNotContain(sources, s => s.HintName.Contains("Endpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidOverlay_IsAnError_NamingTheFileAndTheAction_ReportsGEN016()
    {
        const string invalid = """
            overlay: 1.2.0
            info: { title: t, version: 1.0.0 }
            actions:
              - target: "$.paths['/api/meteringpoints/{id}'].get"
                update: { operationId: getMeteringPoint }
              - target: "$.paths[?@.x =="
                remove: true
            """;

        var (_, diagnostics) = Run(new ApiClientGenerator(), ".atc-rest-api-client", ClientMarker, ("Metering.overlay.yaml", invalid));

        var error = Assert.Single(diagnostics, d => d.Id == "ATC_API_GEN016");
        var message = error.GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains("Metering.overlay.yaml", message, StringComparison.Ordinal);
        Assert.Contains("actions[1]", message, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetThatSelectsNothing_IsAWarning_AndGenerationGoesOn_ReportsGEN017()
    {
        const string stale = """
            overlay: 1.2.0
            info: { title: t, version: 1.0.0 }
            actions:
              - target: "$.paths['/api/meteringpoints/{id}'].get"
                update: { operationId: getMeteringPoint }
              - target: "$.paths['/api/renamed-by-the-vendor']"
                description: Hide the debug endpoint
                remove: true
            """;

        var (sources, diagnostics) = Run(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation", "validateSpecificationStrategy": "Standard", "overlays": ["Metering.overlay.yaml"] }""", ("Metering.overlay.yaml", stale));

        var warning = Assert.Single(diagnostics, d => d.Id == "ATC_API_GEN017");
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("Hide the debug endpoint", warning.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains(sources, s => s.HintName.Contains("GetMeteringPointEndpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void OverlayFileNobodyLists_IsANote_ReportsGEN018()
    {
        var (_, diagnostics) = Run(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation" }""", ("Metering.overlay.yaml", Overlay));

        var note = Assert.Single(diagnostics, d => d.Id == "ATC_API_GEN018");
        Assert.Equal(DiagnosticSeverity.Info, note.Severity);
    }

    [Fact]
    public void EndpointInjection_RegistersTheEndpointsOfTheSpecificationAsTheOverlayChangesIt()
    {
        const string source = """
            namespace Metering.Host;

            [Atc.Rest.Api.SourceGenerator.EndpointRegistration("Metering")]
            public static partial class Endpoints
            {
            }
            """;

        var texts = ImmutableArray.Create<AdditionalText>(
            new CompilationVerificationHarness.InMemoryAdditionalText(".atc-rest-api-client", ClientMarker),
            new CompilationVerificationHarness.InMemoryAdditionalText("Metering.yaml", Spec),
            new CompilationVerificationHarness.InMemoryAdditionalText("Metering.overlay.yaml", Overlay));
        var compilation = CSharpCompilation.Create(
            "Metering.Host",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: false),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new ApiEndpointInjectionGenerator()).AddAdditionalTexts(texts);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var injection = Assert.Single(driver.GetRunResult().GeneratedTrees, t => t.FilePath.EndsWith("Endpoints.EndpointInjection.g.cs", StringComparison.Ordinal)).ToString();

        Assert.Contains("GetMeteringPoint", injection, StringComparison.Ordinal);
        Assert.DoesNotContain("Debug", injection, StringComparison.Ordinal);
    }

    [Fact]
    public void MultiPartSpecification_GetsTheOverlayAfterTheMerge()
    {
        const string basePart = """
            openapi: 3.0.4
            info:
              title: Metering
              version: 1.0.0
            paths: {}
            components:
              schemas:
                MeteringPoint:
                  type: object
                  properties:
                    id:
                      type: string
            """;
        const string part = """
            openapi: 3.0.4
            info:
              title: Metering points
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
            """;

        var (sources, diagnostics) = Run(
            new ApiClientGenerator(),
            ".atc-rest-api-client",
            """{ "generationMode": "EndpointPerOperation", "validateSpecificationStrategy": "Strict", "overlays": ["Metering.overlay.yaml"] }""",
            ("Metering.overlay.yaml", Overlay),
            ("Metering.yaml", basePart),
            ("Metering_Points.yaml", part));

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(sources, s => s.HintName.Contains("GetMeteringPointEndpoint", StringComparison.Ordinal));
    }

    private static (List<(string HintName, string Source)> Sources, List<Diagnostic> Diagnostics) Run(
        IIncrementalGenerator generator,
        string markerFileName,
        string markerContent,
        params (string Path, string Content)[] files)
    {
        var texts = new List<AdditionalText> { new CompilationVerificationHarness.InMemoryAdditionalText(markerFileName, markerContent) };
        if (!files.Any(f => f.Path == "Metering.yaml"))
        {
            texts.Add(new CompilationVerificationHarness.InMemoryAdditionalText("Metering.yaml", Spec));
        }

        texts.AddRange(files.Select(f => new CompilationVerificationHarness.InMemoryAdditionalText(f.Path, f.Content)));

        var compilation = CSharpCompilation.Create(
            "Metering",
            references: CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi: true),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts([.. texts]);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, CancellationToken.None);
        var result = driver.GetRunResult();
        return (
            result.GeneratedTrees.Select(t => (t.FilePath, t.GetText().ToString())).ToList(),
            [.. result.Diagnostics]);
    }
}