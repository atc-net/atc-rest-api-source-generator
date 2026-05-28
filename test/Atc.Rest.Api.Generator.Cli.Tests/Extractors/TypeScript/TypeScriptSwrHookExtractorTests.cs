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

        var result = TypeScriptSwrHookExtractor.Extract(doc!, headerContent: null);
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

        var result = TypeScriptSwrHookExtractor.Extract(doc!, headerContent: null);
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

        var (_, content) = Assert.Single(TypeScriptSwrHookExtractor.Extract(document!, headerContent: null));

        Assert.Contains("* List all items", content, StringComparison.Ordinal);
        Assert.Contains("* @deprecated", content, StringComparison.Ordinal);

        var deprecatedIdx = content.IndexOf("@deprecated", StringComparison.Ordinal);
        var fnIdx = content.IndexOf("export function useListItems", StringComparison.Ordinal);
        Assert.True(deprecatedIdx > 0 && fnIdx > deprecatedIdx, "JSDoc must precede the hook signature.");
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}