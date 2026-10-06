namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

/// <summary>
/// What the hooks must not do on the wire: request <c>/projects/undefined</c> while a second path parameter is
/// missing, skip a query whose id is <c>0</c>, let the caller's <c>enabled</c> replace the guard, resolve
/// <c>mutateAsync</c> before the invalidated queries refetched, page forever on a repeated continuation token,
/// return a partial list in silence when a page fails, or stream from a stale <c>api</c>.
/// </summary>
public class TypeScriptHookHardeningTests
{
    private const string Yaml = """
                                openapi: 3.0.0
                                info: { title: T, version: 1.0.0 }
                                paths:
                                  /orgs/{org}/projects/{project}:
                                    get:
                                      operationId: getProject
                                      parameters:
                                        - { name: org, in: path, required: true, schema: { type: string } }
                                        - { name: project, in: path, required: true, schema: { type: integer } }
                                      responses:
                                        '200': { description: OK, content: { application/json: { schema: { $ref: '#/components/schemas/Project' } } } }
                                  /orgs/{org}/projects:
                                    get:
                                      operationId: listProjects
                                      parameters:
                                        - { name: org, in: path, required: true, schema: { type: string } }
                                        - { name: continuation, in: query, schema: { type: string } }
                                      responses:
                                        '200':
                                          description: A page
                                          content:
                                            application/json:
                                              schema:
                                                allOf:
                                                  - $ref: '#/components/schemas/PaginatedResult'
                                                  - type: object
                                                    properties:
                                                      results: { type: array, items: { $ref: '#/components/schemas/Project' } }
                                    post:
                                      operationId: createProject
                                      parameters:
                                        - { name: org, in: path, required: true, schema: { type: string } }
                                      requestBody:
                                        required: true
                                        content:
                                          application/json:
                                            schema: { $ref: '#/components/schemas/Project' }
                                      responses:
                                        '201': { description: Created, content: { application/json: { schema: { $ref: '#/components/schemas/Project' } } } }
                                  /orgs/{org}/events:
                                    get:
                                      operationId: streamEvents
                                      x-return-async-enumerable: true
                                      parameters:
                                        - { name: org, in: path, required: true, schema: { type: string } }
                                      responses:
                                        '200': { description: Events, content: { text/event-stream: { schema: { $ref: '#/components/schemas/Project' } } } }
                                components:
                                  schemas:
                                    Project:
                                      type: object
                                      required: [name]
                                      properties:
                                        name: { type: string }
                                    PaginatedResult:
                                      type: object
                                      properties:
                                        continuation: { type: string, nullable: true }
                                        results: { type: array, items: {} }
                                """;

    [Fact]
    public void ReactQuery_GuardsEveryPathParameter_AndsTheCallersEnabled_AndAwaitsTheInvalidation()
    {
        var doc = ParseYaml(Yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptReactQueryHookExtractor.Extract(doc, headerContent: null));

        // Every path parameter, by presence (an id of 0 is an id), and the caller's enabled on top of the guard
        Assert.Contains("enabled: org != null && project != null && (options?.enabled ?? true),", content, StringComparison.Ordinal);
        Assert.DoesNotContain("enabled: !!org", content, StringComparison.Ordinal);
        Assert.True(content.IndexOf("...options,", StringComparison.Ordinal) < content.IndexOf("enabled: org != null && project != null", StringComparison.Ordinal));

        // mutateAsync resolves after the invalidated queries refetched
        Assert.Contains("onSuccess: async (data, variables, onMutateResult, context) => {", content, StringComparison.Ordinal);
        Assert.Contains("await queryClient.invalidateQueries({ queryKey: orgsKeys.all });", content, StringComparison.Ordinal);

        // A repeated continuation token ends the infinite query instead of paging forever
        Assert.Contains("getNextPageParam: (lastPage, _allPages, lastPageParam) =>", content, StringComparison.Ordinal);
        Assert.Contains("!== lastPageParam", content, StringComparison.Ordinal);

        // The streaming hook re-runs for a new api instance
        Assert.Contains("}, [enabled, api, org", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Swr_GuardsEveryPathParameter_ByPresence()
    {
        var doc = ParseYaml(Yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(doc, headerContent: null));

        Assert.Contains("org != null && project != null ? orgsKeys.", content, StringComparison.Ordinal);
        Assert.Contains("if (org == null) {", content, StringComparison.Ordinal);
        Assert.DoesNotContain("!!org", content, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!org", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PaginateAll_ThrowsOnAFailedPage_StopsOnARepeatedToken_AndTakesTheParameterNames()
    {
        var result = TypeScriptPaginationHelperExtractor.Generate(headerContent: null);

        Assert.Contains("options?: PaginateAllOptions", result, StringComparison.Ordinal);
        Assert.Contains("const continuationParam = options?.continuationParam ?? 'continuation';", result, StringComparison.Ordinal);
        Assert.Contains("throw 'error' in result ? result.error : new Error(", result, StringComparison.Ordinal);
        Assert.Contains("page.continuation === params[continuationParam]", result, StringComparison.Ordinal);
        Assert.Contains("[continuationParam]: page.continuation", result, StringComparison.Ordinal);
        Assert.Contains("for await (const item of paginateAll(fetcher, initialParams, options))", result, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "hooks.yaml", out var document) ? document : null;
}