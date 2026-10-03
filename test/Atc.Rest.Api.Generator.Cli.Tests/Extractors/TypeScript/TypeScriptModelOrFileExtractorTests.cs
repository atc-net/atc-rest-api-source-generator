namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptModelOrFileExtractorTests
{
    private const string ModelOrFileYaml = """
        openapi: 3.0.3
        info: { title: T, version: 1.0.0 }
        paths:
          /reports/{id}:
            get:
              operationId: getReport
              parameters:
                - name: id
                  in: path
                  required: true
                  schema: { type: string }
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Report' }
                    application/pdf:
                      schema: { type: string, format: binary }
        components:
          schemas:
            Report:
              type: object
              properties:
                title: { type: string }
        """;

    private const string JsonOnlyYaml = """
        openapi: 3.0.3
        info: { title: T, version: 1.0.0 }
        paths:
          /reports/{id}:
            get:
              operationId: getReport
              parameters:
                - name: id
                  in: path
                  required: true
                  schema: { type: string }
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Report' }
        components:
          schemas:
            Report:
              type: object
              properties:
                title: { type: string }
        """;

    [Fact]
    public void HasModelOrFile_OperationAnswersJsonOrPdf_ReturnsTrue()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(ModelOrFileYaml);

        Assert.True(TypeScriptModelOrFileExtractor.HasModelOrFile(doc));
    }

    [Fact]
    public void HasModelOrFile_OperationAnswersJsonOnly_ReturnsFalse()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(JsonOnlyYaml);

        Assert.False(TypeScriptModelOrFileExtractor.HasModelOrFile(doc));
    }

    [Fact]
    public void AugmentApiClient_FetchClient_AddsTheRequestOptionOnly()
    {
        var client = TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        var augmented = TypeScriptModelOrFileExtractor.AugmentApiClient(client, isAxios: false);

        Assert.Contains("  modelOrFile?: boolean;", augmented, StringComparison.Ordinal);
        Assert.DoesNotContain("response.data instanceof Blob", augmented, StringComparison.Ordinal);
    }

    [Fact]
    public void AugmentApiClient_AxiosClient_AsksForArrayBufferAndReadsModelBodiesBackAsText()
    {
        var client = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null);

        var augmented = TypeScriptModelOrFileExtractor.AugmentApiClient(client, isAxios: true);

        Assert.Contains("  modelOrFile?: boolean;", augmented, StringComparison.Ordinal);
        Assert.Contains("responseType: options?.responseType === 'blob' || options?.modelOrFile ? 'arraybuffer'", augmented, StringComparison.Ordinal);
        Assert.Contains("if (options?.modelOrFile && response.status >= 200 && response.status < 300", augmented, StringComparison.Ordinal);
        Assert.Contains("let body: unknown = await response.data.text();", augmented, StringComparison.Ordinal);
    }

    [Fact]
    public void AugmentApiClient_PreservesCrLfLineEndings()
    {
        var client = TypeScriptFetchApiClientExtractor.Generate(headerContent: null).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);

        var augmented = TypeScriptModelOrFileExtractor.AugmentApiClient(client, isAxios: false);

        Assert.DoesNotMatch("[^\r]\n", augmented);
        Assert.Contains("  modelOrFile?: boolean;\r\n", augmented, StringComparison.Ordinal);
    }

    [Fact]
    public void AugmentApiClient_AnchorMissing_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => TypeScriptModelOrFileExtractor.AugmentApiClient("export class ApiClient {}", isAxios: false));

        Assert.Contains("no longer has exactly one", ex.Message, StringComparison.Ordinal);
    }
}