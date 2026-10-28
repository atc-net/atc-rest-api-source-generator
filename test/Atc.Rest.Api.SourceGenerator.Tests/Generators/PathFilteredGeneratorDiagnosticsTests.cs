namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// With <c>includePaths</c> / <c>excludePaths</c> in the marker, the diagnostics about operations judge only the
/// operations the filter keeps - the ones that are generated - for a single file and a multi-part specification alike:
/// what is wrong with a left-out operation is not reported, and an error there does not stop the generation. The rules
/// about the document's components still judge them all, as every component schema is generated.
/// </summary>
public sealed class PathFilteredGeneratorDiagnosticsTests
{
    private const string Header = """
        openapi: 3.1.1
        info:
          title: Things
          version: 1.0.0
        """;

    private const string ThingsPath = """
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
                        items: { $ref: '#/components/schemas/Thing' }
        """;

    private const string Components = """
        components:
          schemas:
            Thing:
              type: object
              required: [name]
              properties:
                name: { type: string }
        """;

    // A parameter no server can bind (ATC_API_OPR037, an error to a server)
    private const string UnbindableAdminPath = """
          /admin/things:
            get:
              operationId: listAdminThings
              parameters:
                - name: filter
                  in: query
                  schema: { type: object, properties: { name: { type: string } } }
              responses:
                '200': { description: OK }
        """;

    private const string ServerMarker = ".atc-rest-api-server";

    private const string Excluded = """{ "excludePaths": ["/admin/**"] }""";

    [Fact]
    public void Server_AnUnbindableParameterOnAnExcludedPath_IsNoErrorAndTheKeptOperationsAreGenerated()
    {
        var spec = Header + "\npaths:\n" + ThingsPath + "\n" + UnbindableAdminPath + "\n" + Components;

        var unfiltered = Run(new ApiServerGenerator(), "{}", Refs(false), ServerMarker, ("/project/Things.yaml", spec));
        var filtered = Run(new ApiServerGenerator(), Excluded, Refs(false), ServerMarker, ("/project/Things.yaml", spec));

        Assert.Contains(unfiltered.Diagnostics, d => d.Id == "ATC_API_OPR037" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(filtered.Diagnostics, d => d.Id == "ATC_API_OPR037");
        Assert.Contains(filtered.HintNames, h => h.Contains("ThingsEndpointDefinition", StringComparison.Ordinal));
        Assert.DoesNotContain(filtered.HintNames, h => h.Contains("AdminEndpointDefinition", StringComparison.Ordinal));
    }

    /// <summary>
    /// The multi-part path (the parts merged, then validated): the excluded operation lives in a part file.
    /// </summary>
    [Fact]
    public void Server_MultiPart_AnUnbindableParameterOnAnExcludedPathInAPart_IsNoErrorAndTheKeptOperationsAreGenerated()
    {
        var baseFile = Header + "\npaths:\n" + ThingsPath + "\n" + Components;
        var part = "openapi: 3.1.1\npaths:\n" + UnbindableAdminPath;

        var unfiltered = Run(new ApiServerGenerator(), "{}", Refs(false), ServerMarker, ("/project/Things.yaml", baseFile), ("/project/Things_Admin.yaml", part));
        var filtered = Run(new ApiServerGenerator(), Excluded, Refs(false), ServerMarker, ("/project/Things.yaml", baseFile), ("/project/Things_Admin.yaml", part));

        Assert.Contains(unfiltered.Diagnostics, d => d.Id == "ATC_API_OPR037" && d.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(filtered.Diagnostics, d => d.Id == "ATC_API_OPR037");
        Assert.DoesNotContain(filtered.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(filtered.HintNames, h => h.Contains("ThingsEndpointDefinition", StringComparison.Ordinal));
    }

    [Fact]
    public void Server_AStringBadRequestOnAnExcludedPath_ReportsNoSRV002()
    {
        const string adminPath = """
              /admin/things:
                post:
                  operationId: createAdminThing
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema: { $ref: '#/components/schemas/Thing' }
                  responses:
                    '201': { description: Created }
                    '400':
                      description: Bad Request
                      content:
                        text/plain:
                          schema: { type: string }
            """;
        var spec = Header + "\npaths:\n" + ThingsPath + "\n" + adminPath + "\n" + Components;

        var unfiltered = Run(new ApiServerGenerator(), "{}", Refs(true), ServerMarker, ("/project/Things.yaml", spec));
        var filtered = Run(new ApiServerGenerator(), Excluded, Refs(true), ServerMarker, ("/project/Things.yaml", spec));

        Assert.Contains(unfiltered.Diagnostics, d => d.Id == "ATC_API_SRV002");
        Assert.DoesNotContain(filtered.Diagnostics, d => d.Id == "ATC_API_SRV002");
    }

    [Theory]
    [InlineData(".atc-rest-api-server")]
    [InlineData(".atc-rest-api-client")]
    public void ResponseLinksOnAnExcludedPath_ReportNoOPR035(
        string markerFileName)
    {
        const string adminPath = """
              /admin/things:
                get:
                  operationId: listAdminThings
                  responses:
                    '200':
                      description: OK
                      links:
                        next:
                          operationId: listAdminThings
            """;
        var spec = Header + "\npaths:\n" + ThingsPath + "\n" + adminPath + "\n" + Components;
        IIncrementalGenerator Generator() => markerFileName == ".atc-rest-api-server" ? new ApiServerGenerator() : new ApiClientGenerator();

        var unfiltered = Run(Generator(), "{}", Refs(false), markerFileName, ("/project/Things.yaml", spec));
        var filtered = Run(Generator(), Excluded, Refs(false), markerFileName, ("/project/Things.yaml", spec));

        Assert.Contains(unfiltered.Diagnostics, d => d.Id == "ATC_API_OPR035");
        Assert.DoesNotContain(filtered.Diagnostics, d => d.Id == "ATC_API_OPR035");
    }

    /// <summary>
    /// A TypedClient needs Atc.Rest.Client for an operation that uploads a file: one the filter leaves out is not
    /// generated, so it does not need the package.
    /// </summary>
    [Fact]
    public void TypedClient_AFileUploadOnAnExcludedPath_DoesNotRequireAtcRestClient()
    {
        const string adminPath = """
              /admin/things/files:
                post:
                  operationId: uploadAdminThingFile
                  requestBody:
                    required: true
                    content:
                      application/octet-stream:
                        schema: { type: string, format: binary }
                  responses:
                    '204': { description: No Content }
            """;
        var spec = Header + "\npaths:\n" + ThingsPath + "\n" + adminPath + "\n" + Components;
        var references = CompilationVerificationHarness.GetFullFrameworkReferences()
            .Where(r => r is not PortableExecutableReference pe ||
                        !Path.GetFileNameWithoutExtension(pe.FilePath ?? string.Empty).StartsWith("Atc.Rest.Client", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var unfiltered = Run(new ApiClientGenerator(), """{ "generationMode": "TypedClient" }""", references, ".atc-rest-api-client", ("/project/Things.yaml", spec));
        var filtered = Run(new ApiClientGenerator(), """{ "generationMode": "TypedClient", "excludePaths": ["/admin/**"] }""", references, ".atc-rest-api-client", ("/project/Things.yaml", spec));

        Assert.Contains(unfiltered.Diagnostics, d => d.Id == "ATC_API_DEP014");
        Assert.DoesNotContain(filtered.Diagnostics, d => d.Id == "ATC_API_DEP014");
        Assert.Contains(filtered.HintNames, h => h.Contains("Client", StringComparison.Ordinal));
    }

    /// <summary>
    /// ATC_API_SCH022 is about a component schema: the models are generated from every one, whatever the filter keeps,
    /// so a schema only an excluded operation uses is still generated - and still reported.
    /// </summary>
    [Theory]
    [InlineData(".atc-rest-api-server")]
    [InlineData(".atc-rest-api-client")]
    public void ASchemaWithoutRequiredUsedOnlyOnAnExcludedPath_IsStillGenerated_AndReportsSCH022(
        string markerFileName)
    {
        const string adminPath = """
              /admin/things:
                post:
                  operationId: createAdminThing
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema: { $ref: '#/components/schemas/AdminThing' }
                  responses:
                    '204': { description: No Content }
            """;
        const string adminThing = """
                AdminThing:
                  type: object
                  properties:
                    name: { type: string }
            """;
        var spec = Header + "\npaths:\n" + ThingsPath + "\n" + adminPath + "\n" + Components + "\n" + adminThing;
        IIncrementalGenerator generator = markerFileName == ".atc-rest-api-server" ? new ApiServerGenerator() : new ApiClientGenerator();

        var filtered = Run(generator, Excluded, Refs(false), markerFileName, ("/project/Things.yaml", spec));

        var warning = Assert.Single(filtered.Diagnostics, d => d.Id == "ATC_API_SCH022");
        Assert.Contains("'AdminThing'", warning.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains(filtered.HintNames, h => h.Contains(".Models.AdminThing.", StringComparison.Ordinal));
        Assert.DoesNotContain(filtered.HintNames, h => h.Contains("Admin", StringComparison.Ordinal) && !h.Contains(".Models.AdminThing.", StringComparison.Ordinal));
    }

    private static List<MetadataReference> Refs(bool includeMinimalApi)
        => CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi);

    private static (List<Diagnostic> Diagnostics, List<string> HintNames) Run(
        IIncrementalGenerator generator,
        string markerContent,
        IEnumerable<MetadataReference> references,
        string markerFileName,
        params (string Path, string Content)[] yamlFiles)
    {
        var compilation = CSharpCompilation.Create(
            "Things",
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var texts = yamlFiles
            .Select(f => (AdditionalText)new InMemoryAdditionalText(f.Path, f.Content))
            .Append(new InMemoryAdditionalText($"/project/{markerFileName}", markerContent))
            .ToList();

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts([.. texts])
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, CancellationToken.None);

        var hintNames = driver.GetRunResult().Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.HintName)
            .ToList();

        return ([.. diagnostics], hintNames);
    }
}