namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptReactQueryHookExtractorTests
{
    [Fact]
    public void Extract_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptReactQueryHookExtractor.Extract(openApiDoc: null!, headerContent: null));
    }

    [Fact]
    public void Extract_NoPaths_ReturnsEmpty()
    {
        var result = TypeScriptReactQueryHookExtractor.Extract(new OpenApiDocument(), headerContent: null);

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_GetOperation_GeneratesUseQueryHook()
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

        var result = TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null);
        var (fileName, content) = Assert.Single(result);

        Assert.Equal("usePets", fileName);

        // GET → useQuery hook call, not useMutation.
        Assert.Contains("useQuery(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("useMutation", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PostOperation_GeneratesUseMutationHook()
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
                                        schema:
                                          type: object
                                  responses:
                                    '201': { description: Created }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains("useMutation", content, StringComparison.Ordinal);

        // POST hooks must invoke useMutation rather than useQuery — the latter is GET-only.
        // (useQueryClient may still be imported for cache invalidation; we only forbid useQuery as a hook call.)
        Assert.DoesNotContain("useQuery(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_HookFile_PrefixedWithUseAndSegmentName()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /accounts:
                                get:
                                  operationId: listAccounts
                                  responses:
                                    '200': { description: OK }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null);
        var (fileName, _) = Assert.Single(result);

        Assert.Equal("useAccounts", fileName);
    }

    [Fact]
    public void Extract_ImportsTanStackQueryAndApiServiceHook()
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

        var result = TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains("@tanstack/react-query", content, StringComparison.Ordinal);
        Assert.Contains("useApiService", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}