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

    [Fact]
    public void Extract_AsyncEnumerableOperation_EmitsStreamHook()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
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
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null);
        var (_, content) = Assert.Single(result);

        // The hook name carries the `Stream` suffix to signal the different return shape
        // (vs `useListItems` which would be a useQuery wrapping a regular GET).
        Assert.Contains("export function useListItemsStream(", content, StringComparison.Ordinal);

        // The skip-with-comment behavior is gone — this is the regression-guard against §1.
        Assert.DoesNotContain("is skipped", content, StringComparison.Ordinal);

        // Hook surface — issue 001 §2 Option A contract.
        Assert.Contains("AbortController", content, StringComparison.Ordinal);
        Assert.Contains("'idle' | 'streaming' | 'success' | 'error'", content, StringComparison.Ordinal);
        Assert.Contains("return { items, status, error, cancel, reset };", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_AsyncEnumerableOperation_ImportsReactPrimitives()
    {
        // Stream hooks rely on useState / useEffect / useRef / useCallback — the import
        // must appear only when at least one stream hook is emitted (otherwise the file
        // would carry a dead import and trip noUnusedLocals).
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
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
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains(
            "import { useCallback, useEffect, useRef, useState } from 'react';",
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_NoStreamingOps_OmitsReactPrimitiveImport()
    {
        // The React primitive import must NOT appear when there are no streaming hooks —
        // every other hook file would otherwise carry a dead import.
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

        Assert.DoesNotContain("from 'react';", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_AsyncEnumerableWithQuery_StreamHookPassesQueryToClient()
    {
        // Stream hook signature must include the query bag; the body must forward it
        // to the generated client method alongside the AbortSignal.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  x-return-async-enumerable: true
                                  parameters:
                                    - name: filter
                                      in: query
                                      schema:
                                        type: string
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
        var (_, content) = Assert.Single(result);

        Assert.Contains("query?: { filter?: string }", content, StringComparison.Ordinal);
        Assert.Contains("api.items.listItems(query, controller.signal)", content, StringComparison.Ordinal);

        // Query bag must contribute to the effect dependency list so changing the filter
        // restarts the stream instead of staying on the previous filter's data.
        Assert.Contains("const keyDep = JSON.stringify({ query });", content, StringComparison.Ordinal);
        Assert.Contains("}, [enabled, keyDep]);", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_OnlyStreamingOps_OmitsApiErrorImport()
    {
        // Stream hooks surface failures as plain Error instances (no ApiError indirection).
        // When every op on the segment is streaming, ApiError must not be imported —
        // tsconfig noUnusedLocals would otherwise reject the generated file.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
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
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.DoesNotContain("import { ApiError }", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_MixedStreamingAndQuery_IncludesApiErrorImport()
    {
        // The query hook's result-unwrap path throws an ApiError, so the import must be
        // present whenever at least one non-stream hook exists on the segment.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
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
                              /items/count:
                                get:
                                  operationId: getItemsCount
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: integer
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var result = TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null);
        var (_, content) = Assert.Single(result);

        Assert.Contains("import { ApiError } from '../errors/ApiError';", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PathAndQueryParams_QueryKeyIncludesBoth()
    {
        // Old behavior dropped `query` from the key when path params were present, so
        // React Query never refetched when filters changed. The key factory must now
        // carry both the path arg and the query bag, and the hook must pass both to
        // the factory call.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items/{itemId}/logs:
                                get:
                                  operationId: getItemLogs
                                  parameters:
                                    - name: itemId
                                      in: path
                                      required: true
                                      schema: { type: string }
                                    - name: level
                                      in: query
                                      schema: { type: string }
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
        var (_, content) = Assert.Single(result);

        // Key factory signature must accept both path and query bag.
        Assert.Contains(
            "getItemLogs: (itemId: string, query?: { level?: string }) => [...itemsKeys.all, 'getItemLogs', itemId, query] as const,",
            content,
            StringComparison.Ordinal);

        // Hook must hand both args to the factory at the call site.
        Assert.Contains("itemsKeys.getItemLogs(itemId, query)", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}