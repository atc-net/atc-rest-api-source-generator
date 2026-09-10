namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptSwrHookExtractorTests
{
    [Fact]
    public void Extract_NullDocument_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => TypeScriptSwrHookExtractor.Extract(openApiDoc: null, headerContent: null));
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
        Assert.Contains("import useSWR from 'swr';", content, StringComparison.Ordinal);
        Assert.Contains("export function useListItemsStream(", content, StringComparison.Ordinal);
        Assert.Contains("export function useListAllItems(", content, StringComparison.Ordinal);
        Assert.Contains("return useSWR(", content, StringComparison.Ordinal);
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

        Assert.Contains("export function useQueryResources(body: ResourceQuery) {", content, StringComparison.Ordinal);
        Assert.Contains("useSWR(", content, StringComparison.Ordinal);
        Assert.Contains("'queryResources', body] as const,", content, StringComparison.Ordinal);
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

        Assert.Contains("export function useLinkResource()", content, StringComparison.Ordinal);
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

        Assert.Contains("return api.pets.createPets();", content, StringComparison.Ordinal);
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

        Assert.Contains("arg as never", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}