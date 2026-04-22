namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptSwrHookExtractorTests
{
    [Fact]
    public void Extract_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptSwrHookExtractor.Extract(openApiDoc: null!, headerContent: null));
    }

    [Fact]
    public void Extract_NoPaths_ReturnsEmpty()
    {
        var result = TypeScriptSwrHookExtractor.Extract(new OpenApiDocument(), headerContent: null);

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_GetOperation_GeneratesUseSWRHook()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptSwrHookExtractor.Extract(doc!, headerContent: null);
        var (fileName, content) = Assert.Single(result);

        Assert.Equal("usePets", fileName);

        // GET → useSWR, not the mutation variant.
        Assert.Contains("useSWR", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PostOperation_GeneratesUseSWRMutationHook()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /pets:
                                post:
                                  operationId: createPet
                                  requestBody:
                                    content:
                                      application/json:
                                        schema: { type: object }
                                  responses:
                                    '201': { description: Created }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptSwrHookExtractor.Extract(doc!, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains("useSWRMutation", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ImportsSwrPackages()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200': { description: OK }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptSwrHookExtractor.Extract(doc!, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains("from 'swr'", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}