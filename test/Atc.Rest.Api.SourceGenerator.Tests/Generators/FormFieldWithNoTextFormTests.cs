namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A form field that is an object, a map or a composition (or a list of one) has no text form: both C# clients leave
/// it out instead of sending its <c>ToString()</c>, and a server generator reports <c>ATC_API_OPR037</c> as an error
/// instead of emitting code that does not compile.
/// </summary>
public sealed class FormFieldWithNoTextFormTests
{
    public static TheoryData<string, string> Cases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var mediaType in new[] { "multipart/form-data", "application/x-www-form-urlencoded" })
            {
                data.Add(mediaType, "{ $ref: '#/components/schemas/Address' }");
                data.Add(mediaType, "{ type: array, items: { $ref: '#/components/schemas/Address' } }");
                data.Add(mediaType, "{ $ref: '#/components/schemas/Composed' }");
                data.Add(mediaType, "{ $ref: '#/components/schemas/Either' }");
                data.Add(mediaType, "{ type: object, additionalProperties: { type: string } }");
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Server_ReportsAnError_AndEmitsNoCodeThatDoesNotCompile(
        string mediaType,
        string fieldYaml)
    {
        var (diagnostics, sources) = Run(new ApiServerGenerator(), ".atc-rest-api-server", "{}", includeMinimalApi: true, Yaml(mediaType, fieldYaml));

        var diagnostic = Assert.Single(diagnostics, d => d.Id == Generator.RuleIdentifiers.ParameterValueNotBindable);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("field 'field'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Clients_LeaveTheFieldOut(
        string mediaType,
        string fieldYaml)
    {
        foreach (var mode in new[] { "TypedClient", "EndpointPerOperation" })
        {
            var (diagnostics, sources) = Run(new ApiClientGenerator(), ".atc-rest-api-client", $$"""{ "generationMode": "{{mode}}" }""", includeMinimalApi: false, Yaml(mediaType, fieldYaml));

            var diagnostic = Assert.Single(diagnostics, d => d.Id == Generator.RuleIdentifiers.ParameterValueNotBindable);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources));

            var sent = sources
                .SelectMany(s => s.Source.Split('\n'))
                .Where(l => l.Contains("new KeyValuePair<string, string>(", StringComparison.Ordinal) ||
                            l.Contains("new StringContent(", StringComparison.Ordinal) ||
                            l.Contains("WithFormField(", StringComparison.Ordinal))
                .ToList();
            Assert.Contains(sent, l => l.Contains("\"name\"", StringComparison.Ordinal));
            Assert.DoesNotContain(sent, l => l.Contains("\"field\"", StringComparison.Ordinal));
        }
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, List<(string HintName, string Source)> Sources) Run(
        IIncrementalGenerator generator,
        string markerFileName,
        string markerContent,
        bool includeMinimalApi,
        string yaml)
    {
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new CompilationVerificationHarness.InMemoryAdditionalText("Forms.yaml", yaml),
            new CompilationVerificationHarness.InMemoryAdditionalText(markerFileName, markerContent));

        var compilation = CSharpCompilation.Create(
            "Forms",
            references: CompilationVerificationHarness.GetFullFrameworkReferences(includeMinimalApi),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = (CSharpGeneratorDriver)CSharpGeneratorDriver
            .Create(generator)
            .AddAdditionalTexts(additionalTexts)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics, CancellationToken.None);

        return (diagnostics, driver.GetRunResult().GeneratedTrees.Select(t => (t.FilePath, t.GetText().ToString())).ToList());
    }

    private static string Yaml(
        string mediaType,
        string fieldYaml)
        => $$"""
            openapi: 3.0.3
            info:
              title: Forms
              version: 1.0.0
            paths:
              /upload:
                post:
                  operationId: upload
                  requestBody:
                    required: true
                    content:
                      {{mediaType}}:
                        schema: { $ref: '#/components/schemas/UploadForm' }
                  responses:
                    '204':
                      description: Done
            components:
              schemas:
                Address:
                  type: object
                  title: Address
                  properties:
                    street: { type: string }
                Other:
                  type: object
                  title: Other
                  properties:
                    code: { type: integer }
                Composed:
                  title: Composed
                  allOf:
                    - $ref: '#/components/schemas/Address'
                    - $ref: '#/components/schemas/Other'
                Either:
                  title: Either
                  oneOf:
                    - $ref: '#/components/schemas/Address'
                    - $ref: '#/components/schemas/Other'
                UploadForm:
                  type: object
                  title: UploadForm
                  properties:
                    name: { type: string }
                    field: {{fieldYaml}}
            """;
}