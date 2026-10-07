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
    public void FetchApiClient_WithModelOrFile_HasTheRequestOptionOnly()
    {
        var client = TypeScriptFetchApiClientExtractor.Generate(headerContent: null, hasModelOrFile: true);

        Assert.Contains("  modelOrFile?: boolean;", client, StringComparison.Ordinal);
        Assert.DoesNotContain("response.data instanceof Blob", client, StringComparison.Ordinal);
    }

    [Fact]
    public void AxiosApiClient_WithModelOrFile_AsksForArrayBufferAndReadsModelBodiesBackAsText()
    {
        var client = TypeScriptAxiosApiClientExtractor.Generate(headerContent: null, hasModelOrFile: true);

        Assert.Contains("  modelOrFile?: boolean;", client, StringComparison.Ordinal);
        Assert.Contains("responseType: options?.responseType === 'blob' || options?.modelOrFile ? 'arraybuffer'", client, StringComparison.Ordinal);
        Assert.Contains("if (options?.modelOrFile && response.status >= 200 && response.status < 300", client, StringComparison.Ordinal);
        Assert.Contains("let body: unknown = await response.data.text();", client, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApiClient_WithoutModelOrFile_HasNoModelOrFileSupport(bool axios)
    {
        var client = axios
            ? TypeScriptAxiosApiClientExtractor.Generate(headerContent: null)
            : TypeScriptFetchApiClientExtractor.Generate(headerContent: null);

        Assert.DoesNotContain("modelOrFile", client, StringComparison.Ordinal);
    }
}