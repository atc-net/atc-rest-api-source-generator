namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A response header that carries a model reads and writes it as the model is generated. With <c>excludePaths</c>
/// leaving out the request that carries <c>Thing</c>, the model is used in responses only and keeps its readOnly
/// <c>id</c> required (<c>int Id</c>) - the header's reader must pass it a value, not an <c>int?</c>. And a model one
/// path segment carries in a header while another segment's operations use it is shared, so both see it.
/// </summary>
public sealed class ResponseHeaderRequiredPathFilterTests
{
    private const string Yaml = """
        openapi: 3.1.1
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
                  headers:
                    X-Thing:
                      schema: { $ref: '#/components/schemas/Thing' }
          /admin/things:
            post:
              operationId: createAdminThing
              requestBody:
                required: true
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Thing' }
              responses:
                '204': { description: No Content }
        components:
          schemas:
            Thing:
              type: object
              title: Thing
              required: [id, name]
              properties:
                id: { type: integer, format: int32, readOnly: true }
                name: { type: string }
        """;

    // Thing is carried by a response header of /pets only; Other by the /others body
    private const string HeaderOnlyYaml = """
        openapi: 3.1.1
        info:
          title: Contoso
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: listPets
              responses:
                '200':
                  description: OK
                  headers:
                    X-Thing:
                      schema: { $ref: '#/components/schemas/Thing' }
          /others:
            get:
              operationId: listOthers
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Other' }
        components:
          schemas:
            Thing:
              type: object
              title: Thing
              required: [name]
              properties:
                name: { type: string }
            Other:
              type: object
              title: Other
              required: [name]
              properties:
                name: { type: string }
        """;

    private const string EndpointPerOperation = "\"generationMode\": \"EndpointPerOperation\"";

    public static TheoryData<string, string, string> Generators => new()
    {
        { nameof(ApiServerGenerator), ".atc-rest-api-server", string.Empty },
        { nameof(ApiClientGenerator), ".atc-rest-api-client", string.Empty },
        { nameof(ApiClientGenerator), ".atc-rest-api-client", EndpointPerOperation },
    };

    [Theory]
    [MemberData(nameof(Generators))]
    public void WithAPathFilter_AHeaderModelsRequiredReadOnlyProperty_IsRequiredAsInTheModel_AndCompiles(
        string generatorName,
        string markerFileName,
        string settings)
    {
        var sources = Run(generatorName, markerFileName, Marker(settings, "\"excludePaths\": [\"/admin/**\"]"), Yaml);

        var thing = Assert.Single(sources, s => s.HintName.Contains(".Models.Thing.", StringComparison.Ordinal)).Source;
        Assert.Contains("int Id", thing, StringComparison.Ordinal);
        Assert.DoesNotContain("int? Id", thing, StringComparison.Ordinal);
        AssertCompiles(sources);
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void AHeaderModelAnotherSegmentUsesAsABody_IsShared_ItsReadOnlyPropertyOptional_AndCompiles(
        string generatorName,
        string markerFileName,
        string settings)
    {
        var sources = Run(generatorName, markerFileName, Marker(settings), Yaml);

        var thing = Assert.Single(sources, s => s.HintName.Contains(".Models.Thing.", StringComparison.Ordinal));
        Assert.Contains("Contoso.Generated.Models.Thing.", thing.HintName, StringComparison.Ordinal);
        Assert.Contains("int? Id", thing.Source, StringComparison.Ordinal);
        AssertCompiles(sources);
    }

    /// <summary>
    /// A model only a response header carries stays a shared model, where it was before.
    /// </summary>
    [Theory]
    [MemberData(nameof(Generators))]
    public void AModelOnlyAResponseHeaderCarries_StaysASharedModel_AndCompiles(
        string generatorName,
        string markerFileName,
        string settings)
    {
        var sources = Run(generatorName, markerFileName, Marker(settings), HeaderOnlyYaml);

        Assert.Contains(sources, s => s.HintName.Contains("Contoso.Generated.Models.Thing.", StringComparison.Ordinal));
        Assert.Contains(sources, s => s.HintName.Contains("Contoso.Generated.Others.Models.Other.", StringComparison.Ordinal));
        AssertCompiles(sources);
    }

    private static string Marker(params string[] settings)
        => "{ " + string.Join(", ", settings.Where(s => s.Length > 0)) + " }";

    private static void AssertCompiles(
        List<(string HintName, string Source)> sources)
    {
        var errors = CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: false);
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    private static List<(string HintName, string Source)> Run(
        string generatorName,
        string markerFileName,
        string markerContent,
        string yaml)
    {
        IIncrementalGenerator generator = generatorName == nameof(ApiServerGenerator)
            ? new ApiServerGenerator()
            : new ApiClientGenerator();

        var compilation = CSharpCompilation.Create(
            "Contoso",
            references: CompilationVerificationHarness.GetFullFrameworkReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(
            [
                new InMemoryAdditionalText("/project/Contoso.yaml", yaml),
                new InMemoryAdditionalText($"/project/{markerFileName}", markerContent),
            ])
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _, CancellationToken.None);

        return driver.GetRunResult().Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => (s.HintName, s.SourceText.ToString()))
            .ToList();
    }
}