// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for streamed operations and the interceptor pipeline of the generated clients (Fetch and Axios): a
/// stream goes through the same pipeline as any other request. Called against a Node http server that answers a stale
/// token with a 401 ProblemDetails and a fresh one with JSON Lines:
/// <list type="bullet">
///   <item><description>the default headers, the access token and the request interceptors apply to the stream request;</description></item>
///   <item><description>the response interceptors see the stream's response, so one that refreshes the token on a 401 and repeats the request makes the stream work;</description></item>
///   <item><description>a failed stream still throws its error (a 401 without a refresh is an <c>ApiError</c> with the problem's detail).</description></item>
/// </list>
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptStreamInterceptorsGateTests
{
    [Fact]
    public void GeneratedClients_Streams_GoThroughTheInterceptors()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "StreamInterceptors.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-stream-interceptors", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            GenerateClient(doc, Path.Combine(workDir, "fetch"), TypeScriptHttpClient.Fetch);
            GenerateClient(doc, Path.Combine(workDir, "axios"), TypeScriptHttpClient.Axios);

            File.WriteAllText(Path.Combine(workDir, "package.json"), PackageJson);
            File.WriteAllText(Path.Combine(workDir, "harness.mts"), Harness);

            NodeWorkspace.Link(workDir);

            var run = NodeToolchain.RunNpx(
                "tsx harness.mts",
                workDir,
                TimeSpan.FromMinutes(3));

            Assert.True(
                run.ExitCode == 0,
                $"Generated client stream-interceptors gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            NodeWorkspace.Delete(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.1
        info:
          title: StreamInterceptors
          version: 1.0.0
        paths:
          /feed/lines:
            get:
              operationId: streamLines
              x-return-async-enumerable: true
              security:
                - bearer: []
              responses:
                '200':
                  description: OK
                  content:
                    application/jsonl:
                      schema:
                        $ref: '#/components/schemas/Tick'
        components:
          securitySchemes:
            bearer:
              type: http
              scheme: bearer
          schemas:
            Tick:
              type: object
              properties:
                n:
                  type: integer
        """;

    private const string PackageJson = """
        {
          "name": "stream-interceptors-gate",
          "version": "0.0.0",
          "private": true,
          "type": "module",
          "dependencies": {
            "axios": "^1.7.0"
          },
          "devDependencies": {
            "@types/node": "^22.0.0",
            "tsx": "^4.19.0",
            "typescript": "^5.6.0"
          }
        }
        """;

    private const string Harness = """
        import axios from 'axios';
        import { createServer } from 'node:http';
        import type { AddressInfo } from 'node:net';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { FeedClient as FetchFeedClient } from './fetch/client/FeedClient';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { FeedClient as AxiosFeedClient } from './axios/client/FeedClient';

        type Seen = { authorization: string; trace: string; tenant: string };
        let seen: Seen[] = [];

        // A stale token gets a 401 ProblemDetails, a fresh one two JSON Lines items.
        const server = createServer((req, res) => {
          seen.push({ authorization: String(req.headers['authorization'] ?? ''), trace: String(req.headers['x-trace'] ?? ''), tenant: String(req.headers['x-tenant'] ?? '') });
          if (req.headers['authorization'] !== 'Bearer fresh') {
            res.writeHead(401, { 'Content-Type': 'application/problem+json' });
            res.end(JSON.stringify({ title: 'Unauthorized', status: 401, detail: 'The token has expired.' }));
            return;
          }

          res.writeHead(200, { 'Content-Type': 'application/jsonl' });
          res.end('{"n":1}\n{"n":2}\n');
        });

        const failures: string[] = [];
        function check(name: string, cond: boolean, detail: string): void {
          if (!cond) {
            failures.push(name + ': ' + detail);
          }
        }

        async function collect(stream: AsyncGenerator<unknown>): Promise<{ items: unknown[]; error?: any }> {
          const items: unknown[] = [];
          try {
            for await (const item of stream) {
              items.push(item);
            }
            return { items };
          } catch (error) {
            return { items, error };
          }
        }

        async function main(): Promise<void> {
          await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
          const baseUrl = 'http://127.0.0.1:' + (server.address() as AddressInfo).port;

          // Fetch: the response interceptor refreshes the token on a 401 and repeats the request.
          {
            let token = 'stale';
            const statuses: number[] = [];
            const api = new FetchApiClient(baseUrl, {
              getAccessToken: () => token,
              defaultHeaders: { 'X-Tenant': 'contoso' },
              requestInterceptors: [(_url, init) => { const headers = new Headers(init.headers); headers.set('X-Trace', 'trace-1'); return { ...init, headers }; }],
              responseInterceptors: [async (response) => {
                statuses.push(response.status);
                if (response.status !== 401) {
                  return response;
                }

                token = 'fresh';
                return fetch(response.url, { headers: { Authorization: 'Bearer fresh', 'X-Trace': 'trace-1', 'X-Tenant': 'contoso' } });
              }],
            });

            seen = [];
            const result = await collect(new FetchFeedClient(api).streamLines());
            check('fetch stream items', result.error === undefined && result.items.length === 2, 'expected two items after the refresh, got ' + JSON.stringify(result.items) + ' ' + String(result.error?.message ?? ''));
            check('fetch response interceptor', statuses[0] === 401, 'expected the interceptor to see the 401, got ' + statuses.join(','));
            check('fetch request pipeline', seen[0]?.authorization === 'Bearer stale' && seen[0]?.trace === 'trace-1' && seen[0]?.tenant === 'contoso', 'expected the token, the default header and the interceptor header, got ' + JSON.stringify(seen[0]));

            const failed = await collect(new FetchFeedClient(new FetchApiClient(baseUrl, { getAccessToken: () => 'stale' })).streamLines());
            check('fetch stream error', failed.error?.status === 401 && failed.error?.message === 'The token has expired.', 'expected an ApiError 401 with the detail, got ' + String(failed.error?.status) + ' ' + String(failed.error?.message));
          }

          // Axios: the response interceptor refreshes the token on a 401 and repeats the request with its config.
          {
            let token = 'stale';
            const statuses: number[] = [];
            const api = new AxiosApiClient(baseUrl, {
              getAccessToken: () => token,
              defaultHeaders: { 'X-Tenant': 'contoso' },
              requestInterceptors: [(config) => { config.headers.set('X-Trace', 'trace-1'); return config; }],
              responseInterceptors: [async (response) => {
                statuses.push(response.status);
                if (response.status !== 401) {
                  return response;
                }

                token = 'fresh';
                response.config.headers.set('Authorization', 'Bearer fresh');
                return axios.request(response.config);
              }],
            });

            seen = [];
            const result = await collect(new AxiosFeedClient(api).streamLines());
            check('axios stream items', result.error === undefined && result.items.length === 2, 'expected two items after the refresh, got ' + JSON.stringify(result.items) + ' ' + String(result.error?.message ?? ''));
            check('axios response interceptor', statuses[0] === 401, 'expected the interceptor to see the 401, got ' + statuses.join(','));
            check('axios request pipeline', seen[0]?.authorization === 'Bearer stale' && seen[0]?.trace === 'trace-1' && seen[0]?.tenant === 'contoso', 'expected the token, the default header and the interceptor header, got ' + JSON.stringify(seen[0]));

            const failed = await collect(new AxiosFeedClient(new AxiosApiClient(baseUrl, { getAccessToken: () => 'stale' })).streamLines());
            check('axios stream error', failed.error?.status === 401 && failed.error?.message === 'The token has expired.', 'expected an ApiError 401 with the detail, got ' + String(failed.error?.status) + ' ' + String(failed.error?.message));
          }

          // The process ends by itself once the server is closed (an explicit exit can trip libuv on Windows)
          server.closeAllConnections();
          await new Promise<void>((resolve) => server.close(() => resolve()));

          if (failures.length > 0) {
            console.error('STREAM INTERCEPTOR FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }

          console.log('ALL STREAM INTERCEPTOR CHECKS PASSED');
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exitCode = 2;
        });
        """;

    private static void GenerateClient(
        OpenApiDocument doc,
        string outputPath,
        TypeScriptHttpClient httpClient)
    {
        Directory.CreateDirectory(outputPath);
        var config = new TypeScriptClientConfig
        {
            HttpClient = httpClient,
            HooksStyle = TypeScriptHooksStyle.None,
            GenerateFileHeaders = true,
            Scaffold = false,
        };

        TypeScriptClientGenerationService.Generate(doc, outputPath, config);
    }

    private static bool IsContinuousIntegration()
        => string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
}