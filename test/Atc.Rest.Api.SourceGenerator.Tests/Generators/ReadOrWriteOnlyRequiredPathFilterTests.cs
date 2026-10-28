namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// ATC_API_SCH029 judges the document the models are generated from. With <c>includePaths</c> / <c>excludePaths</c>
/// in the marker the models come from the path-filtered document, so a schema the full specification uses both ways,
/// but the filtered one in responses only, keeps its required readOnly property required - and the rule says nothing.
/// </summary>
[RuleTest("ATC_API_SCH029")]
public class ReadOrWriteOnlyRequiredPathFilterTests
{
    // Thing is a request on /admin/things (excluded below) and a response on /things
    private const string Yaml = """
        openapi: 3.1.1
        info:
          title: Things
          version: 1.0.0
        paths:
          /things:
            get:
              operationId: listThings
              responses:
                '200':
                  description: The things
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Thing'
          /admin/things:
            post:
              operationId: createThing
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Thing'
              responses:
                '204':
                  description: Created
        components:
          schemas:
            Thing:
              type: object
              title: Thing
              required: [id, name]
              properties:
                id:
                  type: string
                  readOnly: true
                name:
                  type: string
        """;

    public static TheoryData<string, string> Generators => new()
    {
        { nameof(ApiServerGenerator), ".atc-rest-api-server" },
        { nameof(ApiClientGenerator), ".atc-rest-api-client" },
    };

    [Theory]
    [MemberData(nameof(Generators))]
    public void ASchemaUsedInResponsesOnlyAfterTheFilter_KeepsItsReadOnlyPropertyRequired_AndReportsNoSCH029(
        string generatorName,
        string markerFileName)
    {
        var (diagnostics, thing) = Run(generatorName, markerFileName, """{ "excludePaths": ["/admin/**"] }""");

        Assert.DoesNotContain(diagnostics, d => d.Id == "ATC_API_SCH029");
        Assert.Contains("string Id", thing, StringComparison.Ordinal);
        Assert.DoesNotContain("string? Id", thing, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void WithoutTheFilter_TheSchemaIsUsedBothWays_ItsReadOnlyPropertyIsOptional_AndSCH029SaysSo(
        string generatorName,
        string markerFileName)
    {
        var (diagnostics, thing) = Run(generatorName, markerFileName, "{}");

        var warning = Assert.Single(diagnostics, d => d.Id == "ATC_API_SCH029");
        Assert.Contains("'Thing.id'", warning.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Contains("string? Id", thing, StringComparison.Ordinal);
    }

    private static (List<Diagnostic> Diagnostics, string Thing) Run(
        string generatorName,
        string markerFileName,
        string markerContent)
    {
        IIncrementalGenerator generator = generatorName == nameof(ApiServerGenerator)
            ? new ApiServerGenerator()
            : new ApiClientGenerator();

        var compilation = CSharpCompilation.Create(
            "Things",
            references: CompilationVerificationHarness.GetFullFrameworkReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(
            [
                new InMemoryAdditionalText("/project/Things.yaml", Yaml),
                new InMemoryAdditionalText($"/project/{markerFileName}", markerContent),
            ])
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, CancellationToken.None);

        var thing = driver.GetRunResult().Results
            .SelectMany(r => r.GeneratedSources)
            .Single(s => s.HintName.Contains(".Models.Thing.", StringComparison.Ordinal))
            .SourceText
            .ToString();

        return ([.. diagnostics], thing);
    }
}