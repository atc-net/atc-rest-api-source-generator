// ReSharper disable StringLiteralTypo
namespace Atc.Rest.Api.Generator.Cli.Tests.Gates;

/// <summary>
/// Runtime gate for a response whose body stalls after the headers: the generated clients (Fetch and Axios) are
/// called against a Node http server that sends a 200 with the start of a JSON body and never finishes it.
/// <list type="bullet">
///   <item><description>the per-attempt retry timeout (<c>x-retry-timeout-seconds</c>) covers the body read, so the call rejects when it fires;</description></item>
///   <item><description>the caller's AbortSignal cancels the body read of a retried request, not only the request itself;</description></item>
///   <item><description>without retry, an abort while the body is being read rejects with the abort - it is not a <c>parseError</c> result.</description></item>
/// </list>
/// Self-skips without a Node toolchain, but fails under CI where Node is provisioned.
/// </summary>
[Trait("Category", "TypeScriptToolchain")]
public sealed class TypeScriptStalledBodyGateTests
{
    [Fact]
    public void GeneratedClients_StalledBody_TimeoutAndAbortCoverTheBodyRead()
    {
        if (!NodeToolchain.IsAvailable(out var skipReason))
        {
            if (IsContinuousIntegration())
            {
                Assert.Fail(skipReason + " (running under CI where the gate is required)");
            }

            Assert.Skip(skipReason);
        }

        var doc = OpenApiDocumentHelper.TryParseYaml(SpecYaml, "StalledBody.yaml", out var document)
            ? document
            : null;
        Assert.NotNull(doc);

        var workDir = Path.Combine(Path.GetTempPath(), "atc-stalled-body", Guid.NewGuid().ToString("N"));
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
                $"Generated client stalled-body gate failed (exit {run.ExitCode}):\n{run.Output}");
        }
        finally
        {
            TryDeleteDirectory(workDir);
        }
    }

    private const string SpecYaml = """
        openapi: 3.0.3
        info:
          title: StalledBody
          version: 1.0.0
        x-retry-policy: standard
        x-retry-max-attempts: 1
        x-retry-delay-seconds: 0.01
        x-retry-backoff: constant
        x-retry-use-jitter: false
        x-retry-timeout-seconds: 0.3
        paths:
          /stall/timeout:
            get:
              operationId: getTimeout
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
          /stall/abort:
            get:
              operationId: getAbort
              x-retry-timeout-seconds: 10
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Item'
        components:
          schemas:
            Item:
              type: object
              properties:
                id:
                  type: string
        """;

    private const string PackageJson = """
        {
          "name": "stalled-body-gate",
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

    // A server that answers every request with a 200, the start of a JSON body, and then nothing more.
    private const string Harness = """
        import { createServer, type ServerResponse } from 'node:http';
        import type { AddressInfo } from 'node:net';
        import { ApiClient as FetchApiClient } from './fetch/client/ApiClient';
        import { StallClient as FetchStallClient } from './fetch/client/StallClient';
        import { ApiClient as AxiosApiClient } from './axios/client/ApiClient';
        import { StallClient as AxiosStallClient } from './axios/client/StallClient';

        const stalled: ServerResponse[] = [];
        const server = createServer((_req, res) => {
          res.writeHead(200, { 'Content-Type': 'application/json' });
          res.write('{"id":"1",');
          stalled.push(res);
        });

        const failures: string[] = [];

        async function expectRejection(name: string, call: (signal: AbortSignal) => Promise<unknown>, abortAfterMs?: number): Promise<void> {
          const controller = new AbortController();
          if (abortAfterMs !== undefined) {
            setTimeout(() => controller.abort(), abortAfterMs);
          }

          const started = Date.now();
          let hangTimer: ReturnType<typeof setTimeout> | undefined;
          const outcome = await Promise.race([
            call(controller.signal).then(
              (result: any) => 'returned ' + String(result?.status),
              (error: any) => 'threw ' + String(error?.name ?? error)),
            new Promise<string>((resolve) => { hangTimer = setTimeout(() => resolve('hung'), 4000); }),
          ]);
          clearTimeout(hangTimer);
          const elapsed = Date.now() - started;

          if (!outcome.startsWith('threw ') || elapsed > 2000) {
            failures.push(name + ': ' + outcome + ' after ' + elapsed + ' ms (expected a rejection within 2000 ms)');
          } else {
            console.log(name + ': ' + outcome + ' after ' + elapsed + ' ms');
          }
        }

        async function main(): Promise<void> {
          await new Promise<void>((resolve) => server.listen(0, '127.0.0.1', resolve));
          const baseUrl = 'http://127.0.0.1:' + (server.address() as AddressInfo).port;

          for (const [label, api, apiNoRetry, Client] of [
            ['fetch', new FetchApiClient(baseUrl), new FetchApiClient(baseUrl, { retryPolicy: false }), FetchStallClient],
            ['axios', new AxiosApiClient(baseUrl), new AxiosApiClient(baseUrl, { retryPolicy: false }), AxiosStallClient],
          ] as const) {
            const client = new Client(api as any);
            const clientNoRetry = new Client(apiNoRetry as any);

            // 1. The per-attempt timeout (0.3 s, 1 retry) covers the body read.
            await expectRejection(label + ' timeout covers the body', (signal) => client.getTimeout(signal));

            // 2. The caller's abort (after 0.3 s) cancels the body read of a retried request.
            await expectRejection(label + ' abort covers the body (retry)', (signal) => client.getAbort(signal), 300);

            // 3. Without retry, an abort while reading the body rejects - it is not a parseError result.
            await expectRejection(label + ' abort covers the body (no retry)', (signal) => clientNoRetry.getAbort(signal), 300);
          }

          for (const res of stalled) {
            res.destroy();
          }
          server.close();

          if (failures.length > 0) {
            console.error('STALLED BODY FAILURES:\n' + failures.join('\n'));
            process.exitCode = 1;
            return;
          }
          console.log('ALL STALLED BODY CHECKS PASSED');
        }

        main().catch((e) => {
          console.error('HARNESS ERROR', e);
          process.exit(2);
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