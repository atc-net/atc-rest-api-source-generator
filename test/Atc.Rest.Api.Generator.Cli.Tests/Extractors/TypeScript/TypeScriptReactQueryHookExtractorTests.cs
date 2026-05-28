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

    [Fact]
    public void Extract_OperationIdIsReservedWord_HookNameUsesSanitizedMethod()
    {
        // operationId "delete" → methodName "_delete" → hookName "use_Delete".
        // The hook function name must be a valid TS top-level identifier.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items/{id}:
                                delete:
                                  operationId: delete
                                  parameters:
                                    - name: id
                                      in: path
                                      required: true
                                      schema: { type: string }
                                  responses:
                                    '204': { description: No Content }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null));

        // The client call inside the hook body must reference the sanitized method name —
        // otherwise it would try to call `api.items.delete(...)` which collides with the
        // global `delete` operator at call sites that destructure the client.
        Assert.Contains("api.items._delete(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("api.items.delete(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_OperationIdStartsWithDigit_HookEmittedWithSanitizedMethod()
    {
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: T, version: 1.0.0 }
                            paths:
                              /items:
                                get:
                                  operationId: 1stPage
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema:
                                            type: object
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null));

        Assert.Contains("api.items._1stPage(", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_QueryHook_AcceptsOptionsOmittingQueryKeyAndQueryFn()
    {
        // Every useQuery hook accepts a final options? arg whose type
        // omits the keys the generator already supplies (queryKey, queryFn). Consumers
        // can override staleTime, gcTime, select, refetchOnWindowFocus, etc. without
        // wrapping. The spread sits LAST so caller options win over defaults like
        // `enabled: !!petId`.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /pets/{petId}:
                                get:
                                  operationId: getPet
                                  parameters:
                                    - name: petId
                                      in: path
                                      required: true
                                      schema: { type: string }
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema: { $ref: '#/components/schemas/Pet' }
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null));

        Assert.Contains("import type { UseQueryOptions } from '@tanstack/react-query';", content, StringComparison.Ordinal);
        Assert.Contains(
            "options?: Omit<UseQueryOptions<Pet, ApiError>, 'queryKey' | 'queryFn'>",
            content,
            StringComparison.Ordinal);
        Assert.Contains("...options,", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_MutationHook_AcceptsOptionsOmittingMutationFnAndComposesOnSuccess()
    {
        // Every mutation hook accepts a final options? arg whose type
        // omits the keys the generator already supplies (mutationFn). Consumers
        // can override onSuccess, onError, onSettled, etc. without wrapping. The spread sits LAST so caller options win over defaults like
        // `onSuccess: (data, variables, context) => { ... }`.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /pets:
                                post:
                                  operationId: createPet
                                  requestBody:
                                    required: true
                                    content:
                                      application/json:
                                        schema: { $ref: '#/components/schemas/Pet' }
                                  responses:
                                    '201':
                                      description: Created
                                      content:
                                        application/json:
                                          schema: { $ref: '#/components/schemas/Pet' }
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null));

        Assert.Contains("import type { UseMutationOptions } from '@tanstack/react-query';", content, StringComparison.Ordinal);
        Assert.Contains(
            "options?: Omit<UseMutationOptions<Pet, ApiError, Pet>, 'mutationFn'>",
            content,
            StringComparison.Ordinal);

        // onSuccess composition: caller's onSuccess runs after the generator's invalidate,
        // even when the caller spreads its own onSuccess via ...options.
        Assert.Contains("options?.onSuccess?.(data, variables, context);", content, StringComparison.Ordinal);

        // Spread order: caller options spread FIRST, then the composed onSuccess wins.
        var spreadIdx = content.IndexOf("...options,", StringComparison.Ordinal);
        var onSuccessIdx = content.IndexOf("onSuccess: (data, variables, context)", StringComparison.Ordinal);
        Assert.True(
            spreadIdx > 0 &&
            onSuccessIdx > 0 &&
            spreadIdx < onSuccessIdx,
            "Caller options must be spread before the composed onSuccess so the cache invalidation always runs.");
    }

    [Fact]
    public void Extract_DeleteMutationHook_TDataIsVoid_TVariablesMatchesPathParam()
    {
        // Delete (or any void/204-only mutation) has TData = void in the options generic.
        // TVariables comes from the path param when no body is present.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /pets/{petId}:
                                delete:
                                  operationId: deletePet
                                  parameters:
                                    - name: petId
                                      in: path
                                      required: true
                                      schema: { type: string }
                                  responses:
                                    '204': { description: Deleted }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null));

        Assert.Contains(
            "options?: Omit<UseMutationOptions<void, ApiError, string>, 'mutationFn'>",
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_SuspenseMode_EmitsUseSuspenseQueryAndOmitsEnabled()
    {
        // Every useQuery hook accepts a final options? arg whose type
        // follows the underlying hook function. Suspense mode swaps useQuery for useSuspenseQuery
        // and skips the `enabled: !!petId` guard since suspense throws a promise rather than
        // conditionally executing.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /pets/{petId}:
                                get:
                                  operationId: getPet
                                  parameters:
                                    - name: petId
                                      in: path
                                      required: true
                                      schema: { type: string }
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema: { $ref: '#/components/schemas/Pet' }
                            components:
                              schemas:
                                Pet:
                                  type: object
                                  properties:
                                    id: { type: string }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptReactQueryHookExtractor.Extract(
            doc!,
            headerContent: null,
            hooksMode: TypeScriptHooksMode.Suspense));

        Assert.Contains("import { useSuspenseQuery } from '@tanstack/react-query';", content, StringComparison.Ordinal);
        Assert.Contains("import type { UseSuspenseQueryOptions } from '@tanstack/react-query';", content, StringComparison.Ordinal);
        Assert.Contains("return useSuspenseQuery({", content, StringComparison.Ordinal);
        Assert.Contains(
            "options?: Omit<UseSuspenseQueryOptions<Pet, ApiError>, 'queryKey' | 'queryFn'>",
            content,
            StringComparison.Ordinal);

        // Suspense skips conditional enabling — the boundary catches the in-flight promise.
        Assert.DoesNotContain("enabled: !!petId", content, StringComparison.Ordinal);

        // Pure Suspense mode keeps the canonical hook name (no suffix).
        Assert.Contains("export function useGetPet(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("useGetPetSuspense", content, StringComparison.Ordinal);
        Assert.DoesNotContain("import { useQuery", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_BothMode_EmitsStandardAndSuspenseSiblings()
    {
        // Every useQuery hook accepts a final options? arg whose type
        // follows the underlying hook function. --hooks-mode Both emits two hooks per query op so apps can mix
        // suspense-boundary call sites with imperative ones. The suspense variant gets
        // a "Suspense" suffix to coexist.
        const string yaml = """
                            openapi: 3.0.0
                            info: { title: t, version: '1' }
                            paths:
                              /pets:
                                get:
                                  operationId: listPets
                                  responses:
                                    '200':
                                      description: OK
                                      content:
                                        application/json:
                                          schema: { $ref: '#/components/schemas/Pets' }
                            components:
                              schemas:
                                Pets:
                                  type: object
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptReactQueryHookExtractor.Extract(
            doc!,
            headerContent: null,
            hooksMode: TypeScriptHooksMode.Both));

        Assert.Contains("import { useQuery, useSuspenseQuery } from '@tanstack/react-query';", content, StringComparison.Ordinal);
        Assert.Contains("export function useListPets(", content, StringComparison.Ordinal);
        Assert.Contains("export function useListPetsSuspense(", content, StringComparison.Ordinal);
        Assert.Contains("return useQuery({", content, StringComparison.Ordinal);
        Assert.Contains("return useSuspenseQuery({", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PaginatedStreamingOperation_EmitsBothStreamAndInfiniteHooks()
    {
        // Operations with x-return-async-enumerable + allOf(PaginationResult, ...) get
        // BOTH the streaming hook (Option A) AND a useInfiniteQuery sibling (Option B)
        // that consumes a synthesized non-streaming Page companion on the client.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: t, version: '1' }
                            paths:
                              /items/pages:
                                get:
                                  operationId: listItemPages
                                  x-return-async-enumerable: true
                                  parameters:
                                    - name: pageSize
                                      in: query
                                      schema: { type: integer }
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
                                Item:
                                  type: object
                                  properties:
                                    id: { type: string }
                                PaginationResult:
                                  type: object
                                  properties:
                                    continuationToken:
                                      type: string
                                      nullable: true
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var (_, content) = Assert.Single(TypeScriptReactQueryHookExtractor.Extract(doc!, headerContent: null));

        // Streaming hook stays (Option A) for the "show everything as it arrives" UX.
        Assert.Contains("export function useListItemPagesStream(", content, StringComparison.Ordinal);

        // Infinite hook arrives (Option B) for the "paginated with fetchNextPage" UX.
        Assert.Contains("import { useInfiniteQuery", content, StringComparison.Ordinal);
        Assert.Contains("export function useListItemPagesInfinite(", content, StringComparison.Ordinal);
        Assert.Contains("return useInfiniteQuery({", content, StringComparison.Ordinal);

        // Continuation header is threaded through from useInfiniteQuery's pageParam.
        Assert.Contains("pageParam ? { 'x-continuation': pageParam } : undefined", content, StringComparison.Ordinal);

        // getNextPageParam wires the next continuationToken into the next iteration.
        Assert.Contains("getNextPageParam: (lastPage) => lastPage.continuationToken", content, StringComparison.Ordinal);

        // The key factory has an entry for the infinite hook so its cache lives under
        // the segment's `all` prefix.
        Assert.Contains("listItemPagesInfinite:", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_PaginatedStreamingOperation_ClientGainsPageCompanion()
    {
        // The client extractor emits a `<methodName>Page` non-streaming companion
        // alongside the existing async-generator method. The hook above relies on this.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: t, version: '1' }
                            paths:
                              /items/pages:
                                get:
                                  operationId: listItemPages
                                  x-return-async-enumerable: true
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

        var (_, content) = Assert.Single(TypeScriptClientExtractor.Extract(doc!, headerContent: null));

        // Streaming async-generator stays.
        Assert.Contains("async *listItemPages(", content, StringComparison.Ordinal);

        // Page companion adds the synthesized continuation header and returns the per-op
        // result type, not the streaming item type.
        Assert.Contains("async listItemPagesPage(", content, StringComparison.Ordinal);
        Assert.Contains("'x-continuation'?: string", content, StringComparison.Ordinal);
        Assert.Contains("Promise<ListItemPagesPageResult>", content, StringComparison.Ordinal);
        Assert.Contains("data: PaginationResult<Item>", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_OperationWithSummaryAndDeprecated_EmitsJsDocAboveHook()
    {
        // Hooks must surface the same JSDoc as the client method.
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

        var (_, content) = Assert.Single(TypeScriptReactQueryHookExtractor.Extract(document!, headerContent: null));

        Assert.Contains("* List all items", content, StringComparison.Ordinal);
        Assert.Contains("* @deprecated", content, StringComparison.Ordinal);

        var deprecatedIdx = content.IndexOf("@deprecated", StringComparison.Ordinal);
        var fnIdx = content.IndexOf("export function useListItems", StringComparison.Ordinal);
        Assert.True(deprecatedIdx > 0 && fnIdx > deprecatedIdx, "JSDoc must precede the hook signature.");
    }

    [Fact]
    public void Extract_OperationWithoutSummaryOrDeprecated_DoesNotEmitJsDocAboveHook()
    {
        // Regression guard: ops with no useful metadata must not produce a leading
        // JSDoc block — the @deprecated path must not fire on its own.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: t, version: '1' }
                            paths:
                              /items:
                                get:
                                  operationId: listItems
                                  responses:
                                    '200': { description: OK }
                            """;
        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        var (_, content) = Assert.Single(TypeScriptReactQueryHookExtractor.Extract(document!, headerContent: null));

        var fnIdx = content.IndexOf("export function useListItems", StringComparison.Ordinal);
        var before = content[..fnIdx];

        // The line immediately above the export must not be a JSDoc closer.
        var lastNewlineBefore = before.LastIndexOf('\n');
        var prevNewline = before.LastIndexOf('\n', lastNewlineBefore - 1);
        var lineAbove = before[(prevNewline + 1)..lastNewlineBefore];
        Assert.DoesNotContain("*/", lineAbove, StringComparison.Ordinal);
        Assert.DoesNotContain("/**", lineAbove, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}