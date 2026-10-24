namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptSwrHookExtractorTests
{
    [Fact]
    public void Extract_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptSwrHookExtractor.Extract(openApiDoc: null, headerContent: null));
    }

    /// <summary>
    /// HEAD, OPTIONS and TRACE read nothing a component renders and change nothing: they get no hook - least of all
    /// a mutation that invalidates the cached queries - while the GET next to them keeps its own.
    /// </summary>
    [Theory]
    [InlineData("head", "Head")]
    [InlineData("options", "Options")]
    [InlineData("trace", "Trace")]
    public void Extract_ASafeMetadataVerb_GetsNoHook(
        string method,
        string name)
    {
        var yaml = $$"""
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
                         {{method}}:
                           operationId: {{method}}Pets
                           responses:
                             '200':
                               description: OK
                     """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(doc, headerContent: null));

        Assert.Contains("useListPets", content, StringComparison.Ordinal);
        Assert.DoesNotContain($"use{name}Pets", content, StringComparison.Ordinal);
        Assert.DoesNotContain($"{method}Pets(", content, StringComparison.Ordinal);
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

        var result = TypeScriptSwrHookExtractor.Extract(doc, headerContent: null);
        var (fileName, content) = Assert.Single(result);

        Assert.Equal("usePets", fileName);

        // GET → useSWR, not the mutation variant.
        Assert.Contains("useSWR", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_QueryHook_LeavesRetryToTheApiClient_WhenTheSpecificationDeclaresRetry()
    {
        // The ApiClient retries per operation (x-retry-*); SWR's own error retry would multiply the
        // attempts. The caller's config comes last, so it can still turn it back on.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            x-retry-policy: standard
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

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(doc, headerContent: null));

        Assert.Contains("import useSWR, { type SWRConfiguration } from 'swr';", content, StringComparison.Ordinal);
        Assert.Contains("export function useListPets(config?: SWRConfiguration<string[], ApiError>) {", content, StringComparison.Ordinal);
        Assert.Contains("{ shouldRetryOnError: false, ...config },", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_QueryHook_LeavesRetryToTheApp_WhenTheSpecificationDeclaresNone()
    {
        // Without x-retry-* the ApiClient does not retry: the app's own retry (its SWRConfig) applies
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

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(doc, headerContent: null));

        Assert.DoesNotContain("shouldRetryOnError", content, StringComparison.Ordinal);
        Assert.Contains("    config,", content, StringComparison.Ordinal);
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

        var result = TypeScriptSwrHookExtractor.Extract(doc, headerContent: null);
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

        var result = TypeScriptSwrHookExtractor.Extract(doc, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains("from 'swr'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_StreamingOperation_EmitsStreamHookInsteadOfSkipComment()
    {
        // SWR consumers no longer get the `// Streaming operation X is skipped.`
        // placeholder. A useXxxStream hook is emitted with the same Option A contract
        // as the React Query sibling — useState + useEffect + useRef + AbortController,
        // exposing { items, status, error, cancel, reset }.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: '1' }
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  x-return-async-enumerable: true
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items: { $ref: '#/components/schemas/Item' }
                            components:
                              schemas:
                                Item:
                                  type: object
                                  properties:
                                    id: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptSwrHookExtractor.Extract(doc, headerContent: null);
        var (_, content) = Assert.Single(result);

        // No "is skipped" placeholder.
        Assert.DoesNotContain("is skipped", content, StringComparison.Ordinal);

        // The stream hook is plain React — uses useState/useEffect/useRef/useCallback,
        // NOT useSWR (which doesn't model AsyncGenerator semantics).
        Assert.Contains("import { useCallback, useEffect, useRef, useState } from 'react';", content, StringComparison.Ordinal);
        Assert.Contains("export function useListItemsStream(", content, StringComparison.Ordinal);
        Assert.Contains("const controllerRef = useRef<AbortController | null>(null)", content, StringComparison.Ordinal);
        Assert.Contains("return { items, status, error, cancel, reset };", content, StringComparison.Ordinal);

        // Critical: the streaming hook itself doesn't pipe through useSWR. The SWR
        // import only appears when a non-streaming GET coexists in the same file.
        Assert.DoesNotContain("useSWR(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_StreamingMixedWithQuery_EmitsBothHookKindsAndImports()
    {
        // When a segment mixes streaming and non-streaming GETs, the SAME file imports
        // the React primitives AND useSWR. Each hook stays in its own lane.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: '1' }
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  x-return-async-enumerable: true
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items: { type: string }
                              /items/all:
                                get:
                                  operationId: listAllItems
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

        var result = TypeScriptSwrHookExtractor.Extract(doc, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains("import { useCallback, useEffect, useRef, useState } from 'react';", content, StringComparison.Ordinal);
        Assert.Contains("import useSWR, { type SWRConfiguration } from 'swr';", content, StringComparison.Ordinal);
        Assert.Contains("export function useListItemsStream(", content, StringComparison.Ordinal);
        Assert.Contains("export function useListAllItems(", content, StringComparison.Ordinal);
        Assert.Contains("return useSWR<string[], ApiError>(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_OperationWithSummaryAndDeprecated_EmitsJsDocAboveSwrHook()
    {
        // SWR hooks must surface the same JSDoc as the React Query
        // hooks and the client method — keeps the IDE strikethrough consistent
        // regardless of which hook style consumers pick.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: t, version: '1' }
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  summary: List all items
                                  deprecated: true
                                  responses:
                                    '200': { description: OK }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(document, headerContent: null));

        Assert.Contains("* List all items", content, StringComparison.Ordinal);
        Assert.Contains("* @deprecated", content, StringComparison.Ordinal);

        var deprecatedIdx = content.IndexOf("@deprecated", StringComparison.Ordinal);
        var fnIdx = content.IndexOf("export function useListItems", StringComparison.Ordinal);
        Assert.True(deprecatedIdx > 0 && fnIdx > deprecatedIdx, "JSDoc must precede the hook signature.");
    }

    [Fact]
    public void Extract_BrandedIdsEnabled_TypesStreamHookPathParamAndImportsBrand()
    {
        // SWR's streaming hook uses real path-param names; its standard SWR hooks fall
        // back to a hardcoded `(id: string)` and don't participate in branding.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: '1' }
                            paths:
                              /items/{itemId}:
                                get:
                                  operationId: streamItem
                                  x-return-async-enumerable: true
                                  parameters:
                                    - { name: itemId, in: path, required: true, schema: { type: string, format: uuid } }
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

        var result = TypeScriptSwrHookExtractor.Extract(
            doc,
            headerContent: null,
            brandedIds: true);
        var (_, content) = Assert.Single(result);

        Assert.Contains("import type { ItemId } from '../types/BrandedIds';", content, StringComparison.Ordinal);
        Assert.Contains("useStreamItemStream(itemId: ItemId,", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_QueryOperation_GeneratesUseSWRHookTakingTheBody()
    {
        // The OpenAPI 3.2 QUERY method is a read that carries its criteria in the request
        // body. It belongs in useSWR alongside GET, and the body has to reach both the hook
        // signature and the cache key — otherwise every criteria set collides on one entry.
        const string yaml = """
                            openapi: 3.2.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /resources:
                                query:
                                  operationId: queryResources
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema:
                                          $ref: '#/components/schemas/ResourceQuery'
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: array
                                            items: { type: string }
                            components:
                              schemas:
                                ResourceQuery:
                                  type: object
                                  title: ResourceQuery
                                  properties:
                                    filter: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptSwrHookExtractor.Extract(doc, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains("export function useQueryResources(body: ResourceQuery, config?: SWRConfiguration<string[], ApiError>) {", content, StringComparison.Ordinal);
        Assert.Contains("return useSWR<string[], ApiError>(", content, StringComparison.Ordinal);
        Assert.Contains("'query', body] as const,", content, StringComparison.Ordinal);
        Assert.Contains("body != null ? resourcesKeys.query(body) : null,", content, StringComparison.Ordinal);
        Assert.Contains("api.resources.queryResources(body)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("useSWRMutation", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_CustomVerbOperation_GeneratesUseSWRMutationHook()
    {
        // OpenAPI 3.2 additionalOperations verbs used to match neither the read nor the
        // mutation set, so the operation was skipped and no hook was emitted at all.
        const string yaml = """
                            openapi: 3.2.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /resources:
                                additionalOperations:
                                  LINK:
                                    operationId: linkResource
                                    responses:
                                      '204': { description: Linked }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptSwrHookExtractor.Extract(doc, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains("export function useLinkResource(config?: SWRMutationConfiguration<", content, StringComparison.Ordinal);
        Assert.Contains("useSWRMutation", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_MutationWithNoParametersAndNoBody_CallsClientMethodWithoutArgument()
    {
        // The generated client method for an operation with no path params, no body and no
        // query/header params takes zero arguments. Passing `arg as never` to it is a
        // TypeScript compile error ("Expected 0 arguments, but got 1"), so the emitted
        // mocks/hooks file would not build in the consumer's project.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /pets:
                                post:
                                  operationId: createPets
                                  responses:
                                    '201': { description: Created }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptSwrHookExtractor.Extract(doc, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains("    async () => {", content, StringComparison.Ordinal);
        Assert.Contains("const result = await api.pets.createPets();", content, StringComparison.Ordinal);
        Assert.DoesNotContain("arg as never", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_MutationWithBody_StillForwardsTheArgument()
    {
        // Guard against over-correcting: an operation that does take something must keep
        // forwarding the SWR mutation argument.
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

        var result = TypeScriptSwrHookExtractor.Extract(doc, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains("async (_key, { arg }) => {", content, StringComparison.Ordinal);
        Assert.Contains("const body = arg;", content, StringComparison.Ordinal);
        Assert.Contains("const result = await api.pets.createPet(body);", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_DoesNotImportApiResult_BecauseTheHooksNeverReferenceIt()
    {
        // The generated SWR hooks read result.status and result.data with the type inferred, so
        // ApiResult is never named. Importing it anyway breaks a consumer building with
        // noUnusedLocals or an ESLint no-unused-vars rule — generated code has to be clean under
        // the strict settings people actually use.
        // The import is gated on the operation returning a named model — a bare string[] does not
        // trigger it — so the document has to $ref a schema to reproduce.
        var doc = ParseYaml(ModelReturningYaml);
        Assert.NotNull(doc);

        var result = TypeScriptSwrHookExtractor.Extract(doc, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.DoesNotContain("ApiResult", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_EveryTypeImportIsActuallyUsed()
    {
        // Defence in depth for the whole import block rather than one symbol: each name brought in
        // by an `import type { ... }` must appear somewhere else in the file.
        var doc = ParseYaml(ModelReturningYaml);
        Assert.NotNull(doc);

        var result = TypeScriptSwrHookExtractor.Extract(doc, headerContent: null);
        var (_, content) = Assert.Single(result);

        var lines = content.Split('\n');
        foreach (var line in lines.Where(l => l.StartsWith("import type {", StringComparison.Ordinal)))
        {
            var names = line
                .Split('{')[1]
                .Split('}')[0]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var name in names)
            {
                var usageCount = lines.Count(l => l.Contains(name, StringComparison.Ordinal));
                Assert.True(
                    usageCount > 1,
                    $"'{name}' is imported but never used — the import line is its only occurrence.");
            }
        }
    }

    /// <summary>
    /// A GET returning a named model plus a QUERY taking one, so both the return-type and
    /// body-type import paths are exercised.
    /// </summary>
    private const string ModelReturningYaml = """
                                              openapi: 3.2.0
                                              info: { title: T, version: 1.0.0 }
                                              paths:
                                                /resources:
                                                  get:
                                                    operationId: listResources
                                                    responses:
                                                      '200':
                                                        description: OK
                                                        content:
                                                          application/json:
                                                            schema:
                                                              type: array
                                                              items:
                                                                $ref: '#/components/schemas/Resource'
                                                  query:
                                                    operationId: queryResources
                                                    requestBody:
                                                      required: true
                                                      content:
                                                        application/json:
                                                          schema:
                                                            $ref: '#/components/schemas/ResourceQuery'
                                                    responses:
                                                      '200':
                                                        description: OK
                                                        content:
                                                          application/json:
                                                            schema:
                                                              type: array
                                                              items:
                                                                $ref: '#/components/schemas/Resource'
                                              components:
                                                schemas:
                                                  Resource:
                                                    type: object
                                                    title: Resource
                                                    properties:
                                                      id: { type: string }
                                                  ResourceQuery:
                                                    type: object
                                                    title: ResourceQuery
                                                    properties:
                                                      filter: { type: string }
                                              """;

    [Fact]
    public void Extract_QueryHook_TakesEveryParameterAndGuardsOnThePathParameter()
    {
        // Same shape as the React Query hook: path, query and header parameters flow into the client
        // call, the key carries path + query, and no request is made while the id is missing.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /pets/{petId}:
                                get:
                                  operationId: getPetById
                                  parameters:
                                    - { name: petId, in: path, required: true, schema: { type: string } }
                                    - { name: expand, in: query, schema: { type: boolean } }
                                    - { name: x-tenant, in: header, schema: { type: string } }
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema: { $ref: '#/components/schemas/Pet' }
                            components:
                              schemas:
                                Pet: { type: object, properties: { name: { type: string } } }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(doc, headerContent: null));

        Assert.Contains("import type { Pet } from '../models';", content, StringComparison.Ordinal);
        Assert.Contains("export function useGetPetById(petId: string, query?: { expand?: boolean }, headers?: {", content, StringComparison.Ordinal);
        Assert.Contains("config?: SWRConfiguration<Pet, ApiError>) {", content, StringComparison.Ordinal);
        Assert.Contains("petId != null ? petsKeys.petById(petId, query) : null,", content, StringComparison.Ordinal);
        Assert.Contains("const result = await api.pets.getPetById(petId, query, headers);", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_MutationHook_RevalidatesTheSegmentsQueriesOnSuccess()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /pets/{petId}:
                                delete:
                                  operationId: deletePet
                                  parameters:
                                    - { name: petId, in: path, required: true, schema: { type: string } }
                                  responses:
                                    '204': { description: No Content }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(doc, headerContent: null));

        Assert.Contains("return useSWRMutation<void, ApiError, typeof petsKeys.all, string>(", content, StringComparison.Ordinal);
        Assert.Contains("const petId = arg;", content, StringComparison.Ordinal);
        Assert.Contains("void mutate((cacheKey) => Array.isArray(cacheKey) && cacheKey[0] === 'pets');", content, StringComparison.Ordinal);
        Assert.Contains("config?.onSuccess?.(data, key, currentConfig);", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PaginatedRead_GetsAnInfiniteHookThroughItsContinuationParameter()
    {
        // A plain paginated GET: the response's token property (continuation) goes back through the
        // operation's own continuation query parameter; the hook's own query leaves it out.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items/paginated:
                                get:
                                  operationId: listPaginatedItems
                                  parameters:
                                    - { name: pageSize, in: query, schema: { type: integer } }
                                    - { name: continuation, in: query, schema: { type: string } }
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            allOf:
                                              - $ref: '#/components/schemas/PaginatedResult'
                                              - type: object
                                                properties:
                                                  results:
                                                    type: array
                                                    items:
                                                      $ref: '#/components/schemas/Item'
                            components:
                              schemas:
                                Item: { type: object, properties: { id: { type: string } } }
                                PaginatedResult:
                                  type: object
                                  properties:
                                    continuation: { type: string, nullable: true }
                                    results: { type: array, items: {} }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(doc, headerContent: null));

        Assert.Contains("import useSWRInfinite, { type SWRInfiniteConfiguration } from 'swr/infinite';", content, StringComparison.Ordinal);
        Assert.Contains("export function useListPaginatedItems(query?: { pageSize?: number; continuation?: string }", content, StringComparison.Ordinal);
        Assert.Contains("export function useListPaginatedItemsInfinite(query?: { pageSize?: number }, config?: SWRInfiniteConfiguration<PaginatedResult<Item>, ApiError>)", content, StringComparison.Ordinal);
        Assert.Contains("listPaginatedInfinite: (query?: { pageSize?: number })", content, StringComparison.Ordinal);
        Assert.Contains("if (previousPage && !previousPage.continuation) {", content, StringComparison.Ordinal);
        Assert.Contains("return [...itemsKeys.listPaginatedInfinite(query), previousPage ? (previousPage.continuation ?? null) : null] as const;", content, StringComparison.Ordinal);
        Assert.Contains("api.items.listPaginatedItems({ ...query, continuation: token ?? undefined })", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PaginatedReadWithContinuationHeader_SendsTheTokenAsThatHeader()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items/{shelf}:
                                get:
                                  operationId: listShelfItems
                                  parameters:
                                    - { name: shelf, in: path, required: true, schema: { type: string } }
                                    - { name: x-continuation, in: header, schema: { type: string } }
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            $ref: '#/components/schemas/ItemPage'
                            components:
                              schemas:
                                ItemPage:
                                  type: object
                                  properties:
                                    continuationToken: { type: string, nullable: true }
                                    items: { type: array, items: { type: string } }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(doc, headerContent: null));

        Assert.Contains("export function useListShelfItemsInfinite(shelf: string, config?: SWRInfiniteConfiguration<ItemPage, ApiError>)", content, StringComparison.Ordinal);
        Assert.Contains("if (shelf == null) {", content, StringComparison.Ordinal);
        Assert.Contains("if (previousPage && !previousPage.continuationToken) {", content, StringComparison.Ordinal);
        Assert.Contains("api.items.listShelfItems(shelf, { 'x-continuation': token ?? undefined })", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PaginatedStream_GetsNoInfiniteHook()
    {
        // A stream of pages is one response: there is nothing to page through with a token.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items/pages:
                                get:
                                  operationId: listItemPages
                                  x-return-async-enumerable: true
                                  parameters:
                                    - { name: continuation, in: query, schema: { type: string } }
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            allOf:
                                              - $ref: '#/components/schemas/PaginationResult'
                                              - type: object
                                                properties:
                                                  items:
                                                    type: array
                                                    items:
                                                      $ref: '#/components/schemas/Item'
                            components:
                              schemas:
                                Item: { type: object }
                                PaginationResult:
                                  type: object
                                  properties:
                                    continuationToken: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(doc, headerContent: null));

        Assert.Contains("export function useListItemPagesStream(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinite", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ResponseWithTokenButNoContinuationParameter_GetsNoInfiniteHook()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: object
                                            properties:
                                              continuation: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(doc, headerContent: null));

        Assert.Contains("export function useListItems(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinite", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}