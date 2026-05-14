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

    [Fact]
    public void Extract_QueryParamReferencesEnum_EmitsEnumImport()
    {
        // When a query parameter $refs an enum, the generated use*.ts file must include
        // the enum import; the queryKey factory and useQuery signature both reference
        // the enum type inline.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: Demo
                              version: 1.0.0
                            paths:
                              /people:
                                get:
                                  operationId: listPeople
                                  parameters:
                                    - name: businessLine
                                      in: query
                                      schema:
                                        $ref: '#/components/schemas/BusinessLine'
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items:
                                              $ref: '#/components/schemas/PersonSummary'
                            components:
                              schemas:
                                PersonSummary:
                                  type: object
                                  properties:
                                    id: { type: string }
                                BusinessLine:
                                  type: string
                                  enum: [Alpha, Beta]
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var enumNames = new HashSet<string>(StringComparer.Ordinal) { "BusinessLine" };
        var result = TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null, enumNames);
        var (_, content) = Assert.Single(result);

        Assert.Contains("businessLine?: BusinessLine", content, StringComparison.Ordinal);
        Assert.Contains("import type { BusinessLine } from '../enums';", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_HeaderParams_AppearInHookSignatureButNotInQueryKey()
    {
        // Hooks accept a `headers?:` arg the same way the client class does, BUT headers
        // are deliberately excluded from the queryKey — typical headers like
        // correlation IDs change per request and would needlessly fragment the cache.
        // If a user wants per-header cache splitting they can wrap the hook.
        const string yaml = """
                            openapi: 3.0.0
                            info:
                              title: T
                              version: 1.0.0
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  parameters:
                                    - name: limit
                                      in: query
                                      schema:
                                        type: integer
                                    - name: X-Correlation-Id
                                      in: header
                                      required: true
                                      schema:
                                        type: string
                                  responses:
                                    '200': { description: OK }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null);
        var (_, content) = Assert.Single(result);

        // The hook function signature includes a headers arg.
        Assert.Contains(
            "headers?: { 'X-Correlation-Id': string }",
            content,
            StringComparison.Ordinal);

        // Headers are forwarded to the client method call inside the queryFn.
        Assert.Contains("api.items.listItems(query, headers)", content, StringComparison.Ordinal);

        // The keys factory (everything before the first `export function`) must NOT
        // mention the header name — that's the cache-fragmentation we explicitly avoid.
        var keysFactorySection = content[..content.IndexOf("export function", StringComparison.Ordinal)];
        Assert.DoesNotContain("X-Correlation-Id", keysFactorySection, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}