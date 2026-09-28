// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for the generated SWR hooks (Fetch and Axios): the hooks run in React 18 under jsdom
/// (<c>@testing-library/react</c>) against a mocked transport, with an empty SWR cache per case.
/// <para>
/// Asserts what compilation cannot prove:
/// </para>
/// <list type="bullet">
///   <item><description>a query whose path parameter is missing makes no request; with it, path, query and header values reach the wire;</description></item>
///   <item><description>an error is the client's <c>ApiError</c> after exactly one request, and <c>shouldRetryOnError: true</c> turns SWR's retry back on;</description></item>
///   <item><description>a mutation sends its argument as the body, and a mounted query of the segment fetches again afterwards;</description></item>
///   <item><description>the infinite hook of a paginated read sends the previous page's token through the operation's own continuation query parameter, keeps its other query values, and stops when there is none.</description></item>
/// </list>
/// Self-skips without a Node toolchain, but fails under CI.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptSwrHooksRuntimeGateTests
{
    private const string SpecYaml = """
        openapi: 3.0.3
        info:
          title: Pets
          version: 1.0.0
        paths:
          /pets:
            get:
              operationId: listPets
              parameters:
                - name: limit
                  in: query
                  schema:
                    type: integer
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items:
                          $ref: '#/components/schemas/Pet'
            post:
              operationId: createPet
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Pet'
              responses:
                '201':
                  description: Created
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
          /pets/{petId}:
            get:
              operationId: getPetById
              parameters:
                - name: petId
                  in: path
                  required: true
                  schema:
                    type: string
                - name: expand
                  in: query
                  schema:
                    type: boolean
                - name: x-tenant
                  in: header
                  schema:
                    type: string
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Pet'
                '404':
                  description: Not Found
          /items:
            post:
              operationId: createItem
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Item'
              responses:
                '201':
                  description: Created
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
          /items/pages:
            get:
              operationId: listItemPages
              parameters:
                - name: pageSize
                  in: query
                  schema:
                    type: integer
                - name: continuation
                  in: query
                  schema:
                    type: string
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
            Pet:
              type: object
              properties:
                name:
                  type: string
            Item:
              type: object
              properties:
                id:
                  type: integer
            PaginationResult:
              type: object
              properties:
                continuationToken:
                  type: string
                  nullable: true
        """;

    private const string PackageJson = """
        {
          "name": "swr-hooks-runtime-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "dependencies": {
            "axios": "^1.7.0",
            "react": "^18.3.1",
            "react-dom": "^18.3.1",
            "swr": "^2.2.5"
          },
          "devDependencies": {
            "@testing-library/dom": "^10.4.0",
            "@testing-library/react": "^16.0.0",
            "global-jsdom": "^25.0.0",
            "jsdom": "^25.0.0",
            "tsx": "^4.19.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    private const string Prelude = """
        import 'global-jsdom/register';
        import { createElement, type ReactNode } from 'react';
        import { renderHook, waitFor, act } from '@testing-library/react';
        import { SWRConfig } from 'swr';
        import { ApiProvider } from './generated/hooks/ApiProvider';
        import { useGetPetById, useListPets, useCreatePet } from './generated/hooks/usePets';
        import { useListItemPagesInfinite, useCreateItem } from './generated/hooks/useItems';
        import { ApiError } from './generated/errors/ApiError';

        (globalThis as any).IS_REACT_ACT_ENVIRONMENT = true;

        type Call = { method: string; url: string; headers: Record<string, string>; body: string | undefined };
        const calls: Call[] = [];

        function reply(call: Call): { status: number; body: unknown } {
          calls.push(call);
          const path = call.url.replace('http://localhost', '').split('?')[0];
          if (call.method === 'POST' && path === '/pets') return { status: 201, body: JSON.parse(call.body ?? '{}') };
          if (path === '/pets') return { status: 200, body: [{ name: 'a' }] };
          if (path === '/pets/missing') return { status: 404, body: { title: 'Not Found', status: 404, detail: 'No such pet.' } };
          if (path.startsWith('/pets/')) return { status: 200, body: { name: path.split('/')[2] } };
          if (call.method === 'POST' && path === '/items') return { status: 201, body: JSON.parse(call.body ?? '{}') };
          if (path === '/items/pages') {
            return new URL(call.url).searchParams.get('continuation') === 't1'
              ? { status: 200, body: { continuationToken: null, items: [{ id: 2 }] } }
              : { status: 200, body: { continuationToken: 't1', items: [{ id: 1 }] } };
          }
          return { status: 500, body: {} };
        }

        """;

    // Fetch: the ApiClient calls the global fetch with the full URL (query string included).
    private const string FetchTransport = """
        (globalThis as any).fetch = async (url: string, init?: RequestInit) => {
          const headers: Record<string, string> = {};
          new Headers(init?.headers as HeadersInit | undefined).forEach((value, key) => { headers[key] = value; });
          const r = reply({ method: String(init?.method ?? 'GET').toUpperCase(), url: String(url), headers, body: init?.body as string | undefined });
          const contentType = r.status >= 400 ? 'application/problem+json' : 'application/json';
          return new Response(JSON.stringify(r.body), { status: r.status, headers: { 'Content-Type': contentType } });
        };

        """;

    // Axios: the adapter gets the params separately; they are folded into the URL for the checks.
    private const string AxiosTransport = """
        import axios from 'axios';

        axios.defaults.adapter = (async (config: any) => {
          const headers: Record<string, string> = {};
          const raw = typeof config.headers?.toJSON === 'function' ? config.headers.toJSON() : (config.headers ?? {});
          for (const [key, value] of Object.entries(raw)) { headers[key.toLowerCase()] = String(value); }
          const params = config.params ? new URLSearchParams(Object.entries(config.params).filter(([, v]) => v !== undefined).map(([k, v]) => [k, String(v)])).toString() : '';
          const url = String(config.baseURL ?? '') + String(config.url ?? '') + (params ? '?' + params : '');
          const r = reply({ method: String(config.method ?? 'get').toUpperCase(), url, headers, body: typeof config.data === 'string' ? config.data : config.data === undefined ? undefined : JSON.stringify(config.data) });
          const contentType = r.status >= 400 ? 'application/problem+json' : 'application/json';
          return { data: JSON.stringify(r.body), status: r.status, statusText: '', headers: { 'content-type': contentType }, config, request: {} };
        }) as any;

        """;

    private const string Cases = """
        const failures: string[] = [];
        function check(name: string, cond: boolean, detail: string): void {
          if (!cond) failures.push(name + ': ' + detail);
        }

        // A fresh cache and no deduping per render, so every case starts from nothing.
        function wrapper({ children }: { children: ReactNode }) {
          return createElement(
            SWRConfig,
            { value: { provider: () => new Map(), dedupingInterval: 0 } },
            createElement(ApiProvider, { baseUrl: 'http://localhost', children }),
          );
        }

        const tokenOf = (c: Call) => new URL(c.url).searchParams.get('continuation');
        const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));
        const callsTo = (path: string, method = 'GET') => calls.filter((c) => c.method === method && c.url.replace('http://localhost', '').split('?')[0] === path);

        async function main(): Promise<void> {
          // Q1: no path parameter -> no request.
          const q1 = renderHook(() => useGetPetById(undefined as unknown as string), { wrapper });
          await sleep(50);
          check('Q1 no request', calls.filter((c) => c.url.includes('/pets/')).length === 0, 'expected no request, got ' + calls.length);
          q1.unmount();

          // Q2: path, query and header values reach the wire.
          const q2 = renderHook(() => useGetPetById('p1', { expand: true }, { 'x-tenant': 'acme' }), { wrapper });
          await waitFor(() => { if (q2.result.current.data?.name !== 'p1') throw new Error('pending'); });
          const q2Call = callsTo('/pets/p1')[0];
          check('Q2 query', q2Call?.url.includes('expand=true') === true, 'url ' + q2Call?.url);
          check('Q2 header', q2Call?.headers['x-tenant'] === 'acme', 'headers ' + JSON.stringify(q2Call?.headers));
          q2.unmount();

          // E1: an error is the client's ApiError, after exactly one request (a short retry interval, so SWR's
          // own retry would show up within the wait if the hook left it on).
          const e1 = renderHook(() => useGetPetById('missing', undefined, undefined, { errorRetryInterval: 10 }), { wrapper });
          await waitFor(() => { if (!e1.result.current.error) throw new Error('pending'); });
          await sleep(100);
          const e1Error = e1.result.current.error;
          check('E1 ApiError', e1Error instanceof ApiError, 'error ' + String(e1Error));
          check('E1 detail', (e1Error as ApiError | undefined)?.detail === 'No such pet.', 'detail ' + (e1Error as ApiError | undefined)?.detail);
          check('E1 one request', callsTo('/pets/missing').length === 1, callsTo('/pets/missing').length + ' requests');
          e1.unmount();

          // E2: shouldRetryOnError: true turns SWR's retry back on.
          const before = callsTo('/pets/missing').length;
          const e2 = renderHook(() => useGetPetById('missing', undefined, undefined, { shouldRetryOnError: true, errorRetryInterval: 10, errorRetryCount: 2 }), { wrapper });
          await waitFor(() => { if (callsTo('/pets/missing').length - before < 3) throw new Error('pending'); }, { timeout: 3000 });
          e2.unmount();

          // M1: a mutation sends its argument, then the segment's mounted query fetches again.
          const m1 = renderHook(() => ({ list: useListPets({ limit: 5 }), create: useCreatePet() }), { wrapper });
          await waitFor(() => { if (!m1.result.current.list.data) throw new Error('pending'); });
          const listBefore = callsTo('/pets').length;
          await act(async () => { await m1.result.current.create.trigger({ name: 'new' }); });
          const post = callsTo('/pets', 'POST')[0];
          check('M1 body', post?.body === JSON.stringify({ name: 'new' }), 'body ' + post?.body);
          await waitFor(() => { if (callsTo('/pets').length <= listBefore) throw new Error('pending'); }, { timeout: 3000 });
          m1.unmount();

          // I1: the infinite hook passes the continuation token and stops without one (the first page is not
          // revalidated on setSize, so the page requests are exactly one per page).
          const i1 = renderHook(() => useListItemPagesInfinite({ pageSize: 2 }, { revalidateFirstPage: false }), { wrapper });
          await waitFor(() => { if (i1.result.current.data?.length !== 1) throw new Error('pending'); });
          await act(async () => { await i1.result.current.setSize(2); });
          await waitFor(() => { if (i1.result.current.data?.length !== 2) throw new Error('pending'); });
          const pages = callsTo('/items/pages');
          check('I1 token', pages[1] !== undefined && tokenOf(pages[1]) === 't1', 'urls ' + JSON.stringify(pages.map((c) => c.url)));
          check('I1 first page without token', pages[0] !== undefined && tokenOf(pages[0]) === null, 'url ' + pages[0]?.url);
          check('I1 own query kept', pages.every((c) => new URL(c.url).searchParams.get('pageSize') === '2'), 'urls ' + JSON.stringify(pages.map((c) => c.url)));
          await act(async () => { await i1.result.current.setSize(3); });
          await sleep(100);
          check('I1 stops', callsTo('/items/pages').length === 2, callsTo('/items/pages').length + ' page requests');
          i1.unmount();

          // I2: the same with SWR's defaults (the first page is revalidated on setSize): no request carries
          // a token other than 't1', and a third page is never loaded.
          const i2Start = callsTo('/items/pages').length;
          const i2 = renderHook(() => useListItemPagesInfinite(), { wrapper });
          await waitFor(() => { if (i2.result.current.data?.length !== 1) throw new Error('pending'); });
          await act(async () => { await i2.result.current.setSize(2); });
          await waitFor(() => { if (i2.result.current.data?.length !== 2) throw new Error('pending'); });
          await act(async () => { await i2.result.current.setSize(3); });
          await sleep(100);
          const i2Tokens = callsTo('/items/pages').slice(i2Start).map(tokenOf);
          check('I2 token', i2Tokens.includes('t1'), 'tokens ' + JSON.stringify(i2Tokens));
          check('I2 no other token', i2Tokens.every((t) => t === null || t === 't1'), 'tokens ' + JSON.stringify(i2Tokens));
          check('I2 stops', i2.result.current.data?.length === 2, 'pages ' + i2.result.current.data?.length);
          i2.unmount();

          // I3: a mutation of the segment revalidates its mounted infinite list too (SWR keeps infinite lists
          // under $inf$ keys, which its filter-mode mutate skips).
          const i3 = renderHook(() => ({ list: useListItemPagesInfinite(), create: useCreateItem() }), { wrapper });
          await waitFor(() => { if (i3.result.current.list.data?.length !== 1) throw new Error('pending'); });
          const i3Before = callsTo('/items/pages').length;
          await act(async () => { await i3.result.current.create.trigger({ id: 9 }); });
          await waitFor(() => { if (callsTo('/items/pages').length <= i3Before) throw new Error('pending'); }, { timeout: 3000 });
          i3.unmount();

          if (failures.length > 0) {
            console.error('SWR HOOK FAILURES:\n' + failures.join('\n'));
            process.exit(1);
          }

          console.log('ALL SWR HOOK CHECKS PASSED');
          process.exit(0);
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exit(2);
        });
        """;

    [Fact]
    public void GeneratedSwrHooks_Fetch_BehaveAtRuntime()
        => RunGate(TypeScriptHttpClient.Fetch, FetchTransport);

    [Fact]
    public void GeneratedSwrHooks_Axios_BehaveAtRuntime()
        => RunGate(TypeScriptHttpClient.Axios, AxiosTransport);

    private static void RunGate(
        TypeScriptHttpClient httpClient,
        string transport)
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the behaviour gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "Pets.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-ts-swr-runtime", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            var outputPath = Path.Combine(workDir, "generated");
            Directory.CreateDirectory(outputPath);
            TypeScriptClientGenerationService.Generate(
                doc,
                outputPath,
                new TypeScriptClientConfig
                {
                    HttpClient = httpClient,
                    HooksStyle = TypeScriptHooksStyle.Swr,
                    GenerateFileHeaders = true,
                    Scaffold = false,
                });

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);

            // ESM hoists the transport's imports, so the pieces concatenate as they are.
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Prelude + transport + Cases);

            var install = NodeToolchain.RunNpm(
                "install --no-audit --no-fund --loglevel=error",
                workDir,
                TimeSpan.FromMinutes(5));
            Assert.True(
                install.ExitCode == 0,
                $"npm install failed (exit {install.ExitCode}):\n{install.Output}");

            var run = NodeToolchain.RunNpx(
                "tsx harness.mts",
                workDir,
                TimeSpan.FromMinutes(3));

            Assert.True(
                run.ExitCode == 0,
                $"Generated {httpClient} SWR hooks runtime gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private static bool IsContinuousIntegration()
        => string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }
}