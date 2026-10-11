namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A form field that is an object, a map or a composition (or a list of one) has no text form. In a multipart form it
/// is an application/json part (OpenAPI's default for it; a list of one is a part per item), which both C# clients send
/// and a server reads. In a URL-encoded form it has no encoding: both C# clients leave it out instead of sending its
/// <c>ToString()</c>, and a server generator reports <c>ATC_API_OPR037</c> as an error instead of emitting code that
/// does not compile.
/// </summary>
public sealed class FormFieldWithNoTextFormTests
{
    public static TheoryData<string> Fields
        => new(
            "{ $ref: '#/components/schemas/Address' }",
            "{ type: array, items: { $ref: '#/components/schemas/Address' } }",
            "{ $ref: '#/components/schemas/Composed' }",
            "{ $ref: '#/components/schemas/Either' }",
            "{ type: object, additionalProperties: { type: string } }");

    [Theory]
    [MemberData(nameof(Fields))]
    public void UrlEncoded_Server_ReportsAnError_AndEmitsNoCodeThatDoesNotCompile(
        string fieldYaml)
    {
        var (diagnostics, sources) = Run(new ApiServerGenerator(), ".atc-rest-api-server", "{}", includeMinimalApi: true, Yaml("application/x-www-form-urlencoded", fieldYaml));

        var diagnostic = Assert.Single(diagnostics, d => d.Id == Generator.RuleIdentifiers.ParameterValueNotBindable);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("field 'field'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true));
    }

    [Theory]
    [MemberData(nameof(Fields))]
    public void UrlEncoded_Clients_LeaveTheFieldOut(string fieldYaml)
    {
        foreach (var mode in new[] { "TypedClient", "EndpointPerOperation" })
        {
            var (diagnostics, sources) = Run(new ApiClientGenerator(), ".atc-rest-api-client", $$"""{ "generationMode": "{{mode}}" }""", includeMinimalApi: false, Yaml("application/x-www-form-urlencoded", fieldYaml));

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

    [Theory]
    [MemberData(nameof(Fields))]
    public void Multipart_Server_ReadsTheJsonPart_AndCompiles(string fieldYaml)
    {
        foreach (var useRequestDelegateGenerator in new[] { false, true })
        {
            var (diagnostics, sources) = Run(new ApiServerGenerator(), ".atc-rest-api-server", "{}", includeMinimalApi: true, Yaml("multipart/form-data", fieldYaml));

            Assert.DoesNotContain(diagnostics, d => d.Id == Generator.RuleIdentifiers.ParameterValueNotBindable);
            Assert.Contains(sources, s => s.Source.Contains("WireParameter.Json<", StringComparison.Ordinal));
            if (useRequestDelegateGenerator)
            {
                Assert.NotNull(AotAnalyzerHarness.EmitAndLoadWithRequestDelegateGenerator(sources, includeMinimalApi: true));
            }
            else
            {
                Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources, includeMinimalApi: true));
            }
        }
    }

    [Theory]
    [InlineData("{ $ref: '#/components/schemas/Address' }", false)]
    [InlineData("{ type: array, items: { $ref: '#/components/schemas/Address' } }", true)]
    [InlineData("{ $ref: '#/components/schemas/Composed' }", false)]
    [InlineData("{ $ref: '#/components/schemas/Either' }", false)]
    [InlineData("{ type: object, additionalProperties: { type: string } }", false)]
    public void Multipart_EndpointPerOperationClient_SendsAJsonPart(
        string fieldYaml,
        bool isList)
    {
        var (diagnostics, sources) = Run(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "EndpointPerOperation" }""", includeMinimalApi: false, Yaml("multipart/form-data", fieldYaml));

        Assert.DoesNotContain(diagnostics, d => d.Id == Generator.RuleIdentifiers.ParameterValueNotBindable);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources));

        var lines = sources.SelectMany(s => s.Source.Split('\n')).ToList();
        var jsonPart = Assert.Single(lines, l => l.Contains("WithFormJsonField(", StringComparison.Ordinal));
        Assert.Contains("WithFormJsonField(\"field\", ", jsonPart, StringComparison.Ordinal);
        Assert.Contains(isList ? "(\"field\", item)" : "(\"field\", field)", jsonPart, StringComparison.Ordinal);
        Assert.DoesNotContain(lines, l => l.Contains("WithFormField(\"field\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{ $ref: '#/components/schemas/Address' }", false)]
    [InlineData("{ type: array, items: { $ref: '#/components/schemas/Address' } }", true)]
    [InlineData("{ $ref: '#/components/schemas/Composed' }", false)]
    [InlineData("{ $ref: '#/components/schemas/Either' }", false)]
    [InlineData("{ type: object, additionalProperties: { type: string } }", false)]
    public void Multipart_TypedClient_SendsAJsonPart(
        string fieldYaml,
        bool isList)
    {
        var (diagnostics, sources) = Run(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml("multipart/form-data", fieldYaml));

        Assert.DoesNotContain(diagnostics, d => d.Id == Generator.RuleIdentifiers.ParameterValueNotBindable);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources));

        var lines = sources.SelectMany(s => s.Source.Split('\n')).ToList();
        var jsonPart = Assert.Single(lines, l => l.Contains("ToJsonContent(", StringComparison.Ordinal) && l.Contains("\"field\"", StringComparison.Ordinal));
        Assert.Contains(isList ? "content.Add(jsonSerializerOptions.ToJsonContent(item), \"field\");" : "content.Add(jsonSerializerOptions.ToJsonContent(field), \"field\");", jsonPart, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/xml")]
    public void Multipart_WithANonJsonEncoding_ClientsLeaveTheFieldOut_AServerIsAnError(
        string encodingContentType)
    {
        foreach (var mode in new[] { "TypedClient", "EndpointPerOperation" })
        {
            var (diagnostics, sources) = Run(new ApiClientGenerator(), ".atc-rest-api-client", $$"""{ "generationMode": "{{mode}}" }""", includeMinimalApi: false, Yaml("multipart/form-data", "{ $ref: '#/components/schemas/Address' }", encodingContentType));

            var diagnostic = Assert.Single(diagnostics, d => d.Id == Generator.RuleIdentifiers.ParameterValueNotBindable);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(sources));
            Assert.DoesNotContain(sources, s => s.Source.Contains("\"field\"", StringComparison.Ordinal));
        }

        var (serverDiagnostics, serverSources) = Run(new ApiServerGenerator(), ".atc-rest-api-server", "{}", includeMinimalApi: true, Yaml("multipart/form-data", "{ $ref: '#/components/schemas/Address' }", encodingContentType));
        var error = Assert.Single(serverDiagnostics, d => d.Id == Generator.RuleIdentifiers.ParameterValueNotBindable);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains($"encoding names '{encodingContentType}'", error.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Empty(CompilationVerificationHarness.CompileGeneratedSources(serverSources, includeMinimalApi: true));
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
        string fieldYaml,
        string encodingContentType = "")
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
                        {{(encodingContentType.Length == 0 ? string.Empty : "encoding: { field: { contentType: '" + encodingContentType + "' } }")}}
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