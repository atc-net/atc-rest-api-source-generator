namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptClientExtractorTests
{
    [Fact]
    public void Extract_TextPlainOperation_PassesResponseTypeText()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /reports/text:
                                get:
                                  operationId: getTextReport
                                  responses:
                                    '200':
                                      description: Plain text report
                                      content:
                                        text/plain:
                                          schema:
                                            type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null);

        var (_, content) = Assert.Single(clients);

        // Per-operation method must opt into text parsing AND surface the body as a string,
        // not a Blob (default for non-JSON responses).
        Assert.Contains("responseType: 'text'", content, StringComparison.Ordinal);
        Assert.Contains("Promise<ApiResult<string>>", content, StringComparison.Ordinal);
        Assert.Contains("this.api.request<string>('GET', '/reports/text'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_TextCsvOperation_PassesResponseTypeText()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /exports/csv:
                                get:
                                  operationId: exportCsv
                                  responses:
                                    '200':
                                      description: CSV export
                                      content:
                                        text/csv:
                                          schema:
                                            type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null);
        var (_, content) = Assert.Single(clients);

        Assert.Contains("responseType: 'text'", content, StringComparison.Ordinal);
        Assert.Contains("Promise<ApiResult<string>>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_JsonOperation_DoesNotEmitResponseTypeText()
    {
        // Regression: text branch must not leak into ordinary JSON operations.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  responses:
                                    '200':
                                      description: Items
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              type: string
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null);
        var (_, content) = Assert.Single(clients);

        Assert.DoesNotContain("responseType: 'text'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_BinaryFileDownload_DoesNotEmitResponseTypeText()
    {
        // Regression: file downloads keep responseType: 'blob', not 'text'.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Test API
                              version: 1.0.0
                            paths:
                              /files/{id}:
                                get:
                                  operationId: downloadFile
                                  parameters:
                                    - name: id
                                      in: path
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    '200':
                                      description: Binary
                                      content:
                                        application/octet-stream:
                                          schema:
                                            type: string
                                            format: binary
                            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var clients = TypeScriptClientExtractor.Extract(document!, headerContent: null);
        var (_, content) = Assert.Single(clients);

        Assert.Contains("responseType: 'blob'", content, StringComparison.Ordinal);
        Assert.DoesNotContain("responseType: 'text'", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}